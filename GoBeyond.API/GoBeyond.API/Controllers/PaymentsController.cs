using System.Data;
using System.Text.Json;
using GoBeyond.API.Extensions;
using GoBeyond.API.Utilities;
using GoBeyond.Core.DTOs;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Messaging;
using GoBeyond.Infrastructure.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoBeyond.API.Controllers;

public sealed record PaymentCheckoutDto(PaymentDto Payment, SubscriptionDto Subscription, string Message,
    string Mode, string? ClientSecret, string? PaymentIntentId, string? PublishableKey);

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(GoBeyondDbContext db, StripeGateway stripe,
    IOptions<PaymentOptions> settings, INotificationPublisher notifications) : ControllerBase
{
    private PaymentOptions Options => settings.Value;

    [Authorize(Policy = "ClientOnly")]
    [HttpGet("config")]
    public object Configuration() => new { mode = Options.IsDemo ? "Demo" : "Stripe", publishableKey = Options.PublishableKey };

    [Authorize(Policy = "ClientOnly")]
    [HttpPost("create-intent")]
    public async Task<PaymentCheckoutDto> CreateIntent(CreatePaymentIntentRequestDto request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var subscription = await SubscriptionQuery().FirstOrDefaultAsync(x => x.Id == request.SubscriptionId
            && x.ClientProfile.UserId == User.GetUserId(), ct) ?? throw new InvalidOperationException("Subscription not found.");
        EnsurePayable(subscription);
        var payment = subscription.Payments.OrderByDescending(x => x.Id).FirstOrDefault(x => x.Status != PaymentStatus.Refunded);
        if (payment?.Status == PaymentStatus.Succeeded)
            return Checkout(payment, subscription, "This subscription has already been paid.");
        string? secret = null;
        if (payment is null)
        {
            payment = new Payment
            {
                Subscription = subscription, Amount = subscription.AmountPaid,
                Currency = Options.Currency.ToLowerInvariant(), Status = PaymentStatus.Pending
            };
            if (payment.Amount <= 0) throw new InvalidOperationException("The subscription price must be greater than zero.");
            if (Options.IsDemo) payment.StripePaymentIntentId = $"demo_{Guid.NewGuid():N}";
            else
            {
                var intent = await stripe.CreateAsync(subscription.Id, payment.Amount, subscription.ClientProfile.User.Email, ct);
                payment.StripePaymentIntentId = intent.Id;
                secret = intent.ClientSecret;
            }
            db.Payments.Add(payment);
            await db.SaveChangesAsync(ct);
        }
        else if (!Options.IsDemo)
        {
            if (payment.StripePaymentIntentId.StartsWith("demo_"))
                throw new InvalidOperationException("This checkout was created in demo mode. Create a new subscription for Stripe testing.");
            secret = (await stripe.RetrieveAsync(payment.StripePaymentIntentId, ct)).ClientSecret;
        }
        else if (!payment.StripePaymentIntentId.StartsWith("demo_"))
            throw new InvalidOperationException("A Stripe payment cannot be confirmed in demo mode.");
        await transaction.CommitAsync(ct);
        return Checkout(payment, subscription, Options.IsDemo
            ? "Demo checkout: no money will be charged. Confirm explicitly to continue."
            : "Enter payment details in the secure Stripe payment form.", secret);
    }

    [Authorize(Policy = "ClientOnly")]
    [HttpPost("{paymentId:int}/confirm-demo")]
    public async Task<PaymentCheckoutDto> ConfirmDemo(int paymentId, CancellationToken ct)
    {
        if (!Options.IsDemo) throw new InvalidOperationException("Demo payments are disabled.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var payment = await OwnedPayment(paymentId, ct);
        if (!payment.StripePaymentIntentId.StartsWith("demo_"))
            throw new InvalidOperationException("Only a demo checkout can use this action.");
        EnsurePayable(payment.Subscription);
        await ApplyStatus(payment, "succeeded", ct);
        await transaction.CommitAsync(ct);
        return Checkout(payment, payment.Subscription, "Demo payment confirmed. No money was charged.");
    }

    [Authorize(Policy = "ClientOnly")]
    [HttpPost("{paymentId:int}/refresh")]
    public async Task<PaymentCheckoutDto> Refresh(int paymentId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var payment = await OwnedPayment(paymentId, ct);
        if (!Options.IsDemo)
        {
            var intent = await stripe.RetrieveAsync(payment.StripePaymentIntentId, ct);
            ValidateIntent(payment, intent);
            await ApplyStatus(payment, intent.Status, ct);
        }
        await transaction.CommitAsync(ct);
        return Checkout(payment, payment.Subscription, payment.Status == PaymentStatus.Succeeded
            ? "Payment confirmed." : "Payment has not completed yet.");
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        if (Options.IsDemo) return BadRequest(new { message = "Stripe webhooks are disabled in demo mode." });
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);
        if (!StripeGateway.VerifyWebhook(payload, Request.Headers["Stripe-Signature"].ToString(),
            Options.WebhookSecret, DateTimeOffset.UtcNow))
            return BadRequest(new { message = "Invalid Stripe signature." });
        JsonDocument document;
        try { document = JsonDocument.Parse(payload); }
        catch (JsonException) { return BadRequest(new { message = "Invalid event payload." }); }
        using (document)
        {
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
                return BadRequest(new { message = "Invalid event type." });
            if (type.GetString() is not ("payment_intent.succeeded" or "payment_intent.payment_failed" or "payment_intent.canceled"))
                return Ok(new { received = true });
            if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("object", out var obj)
                || !obj.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String)
                return BadRequest(new { message = "Missing payment intent." });
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var payment = await PaymentQuery().FirstOrDefaultAsync(x => x.StripePaymentIntentId == id.GetString(), ct);
            if (payment is not null)
            {
                // Retrieve the authoritative current state; duplicated or out-of-order events cannot roll back success.
                var intent = await stripe.RetrieveAsync(payment.StripePaymentIntentId, ct);
                ValidateIntent(payment, intent);
                await ApplyStatus(payment, intent.Status, ct);
            }
            await transaction.CommitAsync(ct);
        }
        return Ok(new { received = true });
    }

    private async Task ApplyStatus(Payment payment, string stripeStatus, CancellationToken ct)
    {
        if (payment.Status is PaymentStatus.Succeeded or PaymentStatus.Refunded) return;
        if (stripeStatus == "succeeded")
        {
            payment.Status = PaymentStatus.Succeeded;
            var subscription = payment.Subscription;
            // A late webhook must never revive a cancelled or expired collaboration.
            if (subscription.Status == SubscriptionStatus.Pending && subscription.MentorProfile.User.IsActive
                && subscription.MentorProfile.Status == MentorApprovalStatus.Approved)
            {
                subscription.Status = SubscriptionStatus.Active;
                subscription.StartDate = DateTime.UtcNow;
                subscription.EndDate = subscription.StartDate.AddMonths(1);
                subscription.StripePaymentIntentId = payment.StripePaymentIntentId;
                foreach (var recipient in new[] { subscription.ClientProfile.User, subscription.MentorProfile.User })
                {
                    var title = Options.IsDemo ? "Demo subscription activated" : "Subscription payment confirmed";
                    var body = "The mentor can now prepare the personal training plan.";
                    db.Notifications.Add(new Notification { UserId = recipient.Id, Title = title, Body = body,
                        Type = NotificationType.NewSubscriber, IsRead = false });
                    await notifications.PublishAsync("SubscriptionActivated", recipient.Email, title, body, ct);
                }
            }
        }
        else if (stripeStatus == "canceled") payment.Status = PaymentStatus.Failed;
        await db.SaveChangesAsync(ct);
    }

    private static void ValidateIntent(Payment payment, StripeIntent intent)
    {
        if (intent.Id != payment.StripePaymentIntentId || intent.Amount != StripeGateway.ToMinorUnits(payment.Amount)
            || !intent.Currency.Equals(payment.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The payment amount or currency does not match the subscription.");
    }

    private static void EnsurePayable(Subscription subscription)
    {
        if (subscription.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
            throw new InvalidOperationException("This subscription can no longer be paid.");
        if (!subscription.MentorProfile.User.IsActive || subscription.MentorProfile.Status != MentorApprovalStatus.Approved)
            throw new InvalidOperationException("This mentor is currently unavailable.");
    }

    private PaymentCheckoutDto Checkout(Payment payment, Subscription subscription, string message, string? secret = null)
        => new(DtoMapper.ToPaymentDto(payment), DtoMapper.ToSubscriptionDto(subscription), message,
            Options.IsDemo ? "Demo" : "Stripe", secret, payment.StripePaymentIntentId, Options.PublishableKey);

    private async Task<Payment> OwnedPayment(int id, CancellationToken ct)
        => await PaymentQuery().FirstOrDefaultAsync(x => x.Id == id && x.Subscription.ClientProfile.UserId == User.GetUserId(), ct)
            ?? throw new InvalidOperationException("Payment not found.");

    private IQueryable<Payment> PaymentQuery() => db.Payments
        .Include(x => x.Subscription).ThenInclude(x => x.MentorProfile).ThenInclude(x => x.User)
        .Include(x => x.Subscription).ThenInclude(x => x.ClientProfile).ThenInclude(x => x.User)
        .Include(x => x.Subscription).ThenInclude(x => x.Questionnaire)
        .Include(x => x.Subscription).ThenInclude(x => x.TrainingPlans)
        .Include(x => x.Subscription).ThenInclude(x => x.Payments);

    private IQueryable<Subscription> SubscriptionQuery() => db.Subscriptions
        .Include(x => x.MentorProfile).ThenInclude(x => x.User)
        .Include(x => x.ClientProfile).ThenInclude(x => x.User)
        .Include(x => x.Questionnaire).Include(x => x.Payments).Include(x => x.TrainingPlans);
}
