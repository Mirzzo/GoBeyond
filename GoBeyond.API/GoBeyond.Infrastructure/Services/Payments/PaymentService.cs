using System.Globalization;
using System.Text.Json;
using GoBeyond.Core.DTOs.Subscriptions;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoBeyond.Infrastructure.Services.Payments;

public interface IPaymentService
{
    Task<PaymentIntentDto> CreateIntentAsync(int clientUserId, CreatePaymentIntentRequest request, CancellationToken cancellationToken = default);
    Task<SubscriptionDetailDto> ConfirmAsync(int clientUserId, int paymentId, CancellationToken cancellationToken = default);
    Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stripe plaćanje: klijent dobija client_secret za PaymentSheet, a backend tek nakon provjere
/// PaymentIntent-a na Stripe-u (confirm ili potpisani webhook) označava uplatu uspješnom.
/// Nema demo/lažnog načina plaćanja.
/// </summary>
public sealed class PaymentService(
    GoBeyondDbContext db,
    IPaymentGateway gateway,
    ISubscriptionWorkflow workflow,
    ISubscriptionService subscriptions,
    ILogger<PaymentService> logger) : IPaymentService
{
    public const string NotConfigured = "Stripe plaćanje nije konfigurisano na serveru.";
    public const string NotCompleted = "Plaćanje nije završeno. Pokušajte ponovo.";
    private static readonly string[] ReusableIntentStatuses = ["requires_payment_method", "requires_confirmation", "requires_action"];

    public async Task<PaymentIntentDto> CreateIntentAsync(int clientUserId, CreatePaymentIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!gateway.IsConfigured) throw new ValidationException(NotConfigured);

        var subscription = await db.Subscriptions
            .Include(x => x.ClientProfile).ThenInclude(x => x.User)
            .Include(x => x.MentorProfile).ThenInclude(x => x.User)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == request.SubscriptionId && x.ClientProfile.UserId == clientUserId, cancellationToken)
            ?? throw new NotFoundException(DomainTexts.SubscriptionNotFound);

        var purpose = subscription.Status switch
        {
            SubscriptionStatus.PendingPayment => PaymentPurpose.Initial,
            SubscriptionStatus.Active => PaymentPurpose.Renewal,
            _ => throw new ValidationException("Za ovu pretplatu trenutno nije moguće plaćanje.")
        };
        var mentor = subscription.MentorProfile;
        if (mentor.User.IsDeleted || !mentor.User.IsActive || mentor.Status != MentorApprovalStatus.Approved)
            throw new ValidationException("Mentor trenutno nije dostupan, pa plaćanje nije moguće.");

        // Ako postoji nedovršena uplata istog tipa, ponovo se koristi isti PaymentIntent.
        var pending = subscription.Payments
            .Where(x => x.Status == PaymentStatus.Pending && x.Purpose == purpose)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        if (pending is not null)
        {
            var existing = await gateway.GetPaymentIntentAsync(pending.StripePaymentIntentId, cancellationToken);
            if (ReusableIntentStatuses.Contains(existing.Status))
                return ToDto(pending, existing.ClientSecret);
            pending.Status = PaymentStatus.Failed;
        }

        var intent = await gateway.CreatePaymentIntentAsync(subscription.Price, subscription.ClientProfile.User.Email,
            new Dictionary<string, string>
            {
                ["subscriptionId"] = subscription.Id.ToString(CultureInfo.InvariantCulture),
                ["purpose"] = purpose.ToString()
            }, cancellationToken);

        var payment = new Payment
        {
            SubscriptionId = subscription.Id,
            Amount = subscription.Price,
            Currency = gateway.Currency,
            StripePaymentIntentId = intent.Id,
            Purpose = purpose,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(payment, intent.ClientSecret);
    }

    public async Task<SubscriptionDetailDto> ConfirmAsync(int clientUserId, int paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await PaymentQuery()
            .FirstOrDefaultAsync(x => x.Id == paymentId && x.Subscription.ClientProfile.UserId == clientUserId, cancellationToken)
            ?? throw new NotFoundException("Uplata nije pronađena.");

        if (payment.Status != PaymentStatus.Succeeded)
        {
            if (!gateway.IsConfigured) throw new ValidationException(NotConfigured);
            var intent = await gateway.GetPaymentIntentAsync(payment.StripePaymentIntentId, cancellationToken);

            if (intent.Status != "succeeded" || intent.AmountMinor != StripePaymentGateway.ToMinorUnits(payment.Amount))
            {
                if (intent.Status == "canceled")
                {
                    payment.Status = PaymentStatus.Failed;
                    await db.SaveChangesAsync(cancellationToken);
                }
                throw new ValidationException(NotCompleted);
            }

            await ApplySuccessAsync(payment, cancellationToken);
        }

        return await subscriptions.GetMineByIdAsync(clientUserId, payment.SubscriptionId, cancellationToken);
    }

    public async Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default)
    {
        if (!gateway.VerifyWebhookSignature(payload, signatureHeader, DateTimeOffset.UtcNow))
            throw new ValidationException("Stripe potpis webhook poziva nije ispravan.");

        using var json = JsonDocument.Parse(payload);
        var type = json.RootElement.GetProperty("type").GetString();
        var intentId = json.RootElement.GetProperty("data").GetProperty("object").GetProperty("id").GetString();
        if (string.IsNullOrEmpty(intentId)) return;

        var payment = await PaymentQuery().FirstOrDefaultAsync(x => x.StripePaymentIntentId == intentId, cancellationToken);
        if (payment is null)
        {
            logger.LogInformation("Stripe webhook {Type} for unknown PaymentIntent ignored.", type);
            return;
        }

        switch (type)
        {
            case "payment_intent.succeeded":
                await ApplySuccessAsync(payment, cancellationToken);
                break;
            case "payment_intent.payment_failed" or "payment_intent.canceled" when payment.Status == PaymentStatus.Pending:
                payment.Status = PaymentStatus.Failed;
                await db.SaveChangesAsync(cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Atomski "preuzima" uplatu (Pending → Succeeded samo jednom), pa confirm i webhook
    /// koji stignu istovremeno ne mogu dvaput produžiti pretplatu.
    /// </summary>
    private async Task ApplySuccessAsync(Payment payment, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var claimed = await db.Payments
            .Where(x => x.Id == payment.Id && x.Status != PaymentStatus.Succeeded)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.Status, PaymentStatus.Succeeded).SetProperty(p => p.PaidAt, now), cancellationToken);
        if (claimed == 0) return;

        workflow.ApplySuccessfulPayment(payment, now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private IQueryable<Payment> PaymentQuery() => db.Payments
        .Include(x => x.Subscription).ThenInclude(x => x.ClientProfile).ThenInclude(x => x.User)
        .Include(x => x.Subscription).ThenInclude(x => x.MentorProfile).ThenInclude(x => x.User);

    private PaymentIntentDto ToDto(Payment payment, string clientSecret) => new()
    {
        PaymentId = payment.Id,
        ClientSecret = clientSecret,
        PublishableKey = gateway.PublishableKey,
        Amount = payment.Amount,
        Currency = payment.Currency,
        Purpose = payment.Purpose
    };
}
