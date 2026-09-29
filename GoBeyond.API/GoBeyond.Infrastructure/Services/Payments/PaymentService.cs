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

    /// <summary>
    /// Usklađivanje sa Stripe-om bez webhook-a: Pending uplate kreirane između <paramref name="createdAfter"/> i
    /// <paramref name="createdBefore"/> provjeravaju se na Stripe-u. Vraća broj primijenjenih (ili automatski vraćenih) uplata.
    /// </summary>
    Task<int> ReconcilePendingAsync(DateTime createdBefore, DateTime createdAfter, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stripe plaćanje: klijent dobija client_secret za PaymentSheet, a backend tek nakon provjere
/// PaymentIntent-a na Stripe-u (confirm ili potpisani webhook) označava uplatu uspješnom.
/// Prije primjene se provjerava da PaymentIntent (metadata, iznos, valuta) odgovara uplati.
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
    public const string StillProcessing = "Prethodno plaćanje se još obrađuje. Pokušajte ponovo za nekoliko trenutaka.";
    public const string AlreadyPaid = "Ova uplata je već uspješno izvršena. Osvježite prikaz pretplate.";
    public const string IntentMismatch = "Podaci o plaćanju na Stripe-u ne odgovaraju ovoj uplati, pa plaćanje nije primijenjeno. Kontaktirajte podršku.";

    /// <summary>Stripe statusi u kojima klijent može (ponovo) pokušati plaćanje istim PaymentIntent-om.</summary>
    public static readonly string[] ReusableIntentStatuses = ["requires_payment_method", "requires_confirmation", "requires_action"];

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

        var amountMinor = StripePaymentGateway.ToMinorUnits(subscription.Price);

        // Nedovršena uplata istog tipa: ponovo se koristi isti PaymentIntent dok je to moguće.
        foreach (var pending in subscription.Payments
                     .Where(x => x.Status == PaymentStatus.Pending && x.Purpose == purpose)
                     .OrderByDescending(x => x.CreatedAt).ToList())
        {
            var existing = await gateway.GetPaymentIntentAsync(pending.StripePaymentIntentId, cancellationToken);
            var reusable = ReusableIntentStatuses.Contains(existing.Status);

            if (reusable && existing.AmountMinor == amountMinor)
                return ToDto(pending, existing.ClientSecret);

            if (existing.Status == "succeeded")
            {
                // Uplata je prošla, a potvrda (confirm/webhook) još nije stigla - primijeni je sada.
                await ApplySuccessAsync(pending, existing, cancellationToken);
                throw new ConflictException(AlreadyPaid);
            }

            if (existing.Status == "canceled" || reusable)
            {
                // Otkazan PaymentIntent ili zastario iznos (mentor promijenio cijenu): stara uplata se zatvara.
                pending.Status = PaymentStatus.Failed;
                continue;
            }

            // processing, requires_capture ili nepoznat status: plaćanje još traje - ne kreirati drugo.
            throw new ConflictException(StillProcessing);
        }

        var attempt = subscription.Payments.Count(x => x.Purpose == purpose) + 1;
        var idempotencyKey = CreateIntentIdempotencyKey(subscription, purpose, attempt, amountMinor);
        var intent = await gateway.CreatePaymentIntentAsync(subscription.Price, subscription.ClientProfile.User.Email,
            new Dictionary<string, string>
            {
                [PaymentIntentInfo.MetadataSubscriptionId] = subscription.Id.ToString(CultureInfo.InvariantCulture),
                [PaymentIntentInfo.MetadataPurpose] = purpose.ToString()
            }, idempotencyKey, cancellationToken);

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
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Istovremeni zahtjev sa istim Idempotency-Key-om je već upisao ovaj PaymentIntent.
            db.ChangeTracker.Clear();
            var saved = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.StripePaymentIntentId == intent.Id, cancellationToken)
                        ?? throw new ConflictException(StillProcessing);
            return ToDto(saved, intent.ClientSecret);
        }
        return ToDto(payment, intent.ClientSecret);
    }

    public async Task<SubscriptionDetailDto> ConfirmAsync(int clientUserId, int paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await PaymentQuery()
            .FirstOrDefaultAsync(x => x.Id == paymentId && x.Subscription.ClientProfile.UserId == clientUserId, cancellationToken)
            ?? throw new NotFoundException("Uplata nije pronađena.");

        // Succeeded / Refunded / RefundPending su završna stanja - confirm je idempotentan.
        if (payment.Status is PaymentStatus.Pending or PaymentStatus.Failed)
        {
            if (!gateway.IsConfigured) throw new ValidationException(NotConfigured);
            var intent = await gateway.GetPaymentIntentAsync(payment.StripePaymentIntentId, cancellationToken);

            if (intent.Status != "succeeded")
            {
                if (intent.Status == "canceled" && payment.Status == PaymentStatus.Pending)
                {
                    payment.Status = PaymentStatus.Failed;
                    await db.SaveChangesAsync(cancellationToken);
                }
                throw new ValidationException(NotCompleted);
            }

            await ApplySuccessAsync(payment, intent, cancellationToken);
        }

        return await subscriptions.GetMineByIdAsync(clientUserId, payment.SubscriptionId, cancellationToken);
    }

    public async Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default)
    {
        if (!gateway.VerifyWebhookSignature(payload, signatureHeader, DateTimeOffset.UtcNow))
            throw new ValidationException("Stripe potpis webhook poziva nije ispravan.");

        using var json = JsonDocument.Parse(payload);
        var type = json.RootElement.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
        if (!json.RootElement.TryGetProperty("data", out var data) || !data.TryGetProperty("object", out var obj)) return;
        var intent = PaymentIntentInfo.FromJson(obj);
        if (string.IsNullOrEmpty(intent.Id)) return;

        var payment = await PaymentQuery().FirstOrDefaultAsync(x => x.StripePaymentIntentId == intent.Id, cancellationToken);
        if (payment is null)
        {
            logger.LogInformation("Stripe webhook {Type} for unknown PaymentIntent ignored.", type);
            return;
        }

        switch (type)
        {
            case "payment_intent.succeeded" when payment.Status is PaymentStatus.Pending or PaymentStatus.Failed:
                await ApplySuccessAsync(payment, intent, cancellationToken);
                break;
            case "payment_intent.canceled" when payment.Status == PaymentStatus.Pending:
                payment.Status = PaymentStatus.Failed;
                await db.SaveChangesAsync(cancellationToken);
                break;
            case "payment_intent.payment_failed":
                // Neuspio pokušaj (npr. odbijena kartica): Stripe vraća PaymentIntent u requires_payment_method i on se
                // može ponovo platiti, pa uplata ostaje Pending i create-intent ga ponovo koristi (isto kao bez webhook-a).
                // Da se ovdje označi Failed, sljedeći create-intent bi otvorio drugi PaymentIntent, a stari bi ostao plativ.
                logger.LogInformation("Payment attempt for PaymentIntent {IntentId} failed (status {Status}); payment {PaymentId} stays {PaymentStatus}.",
                    intent.Id, intent.Status, payment.Id, payment.Status);
                break;
        }
    }

    /// <summary>
    /// Pokriva slučaj kad je kartica naplaćena, a potvrda nikad nije stigla (aplikacija ugašena ili prekid mreže prije
    /// POST confirm, klijent otkazao pretplatu prije potvrde, a webhook nije podešen). Uspješna uplata se primjenjuje
    /// istom provjerom kao confirm (ili automatski vraća ako se pretplata u međuvremenu promijenila), a otkazan
    /// PaymentIntent postaje Failed. Greška jedne uplate (npr. Stripe nedostupan) ne zaustavlja ostale.
    /// </summary>
    public async Task<int> ReconcilePendingAsync(DateTime createdBefore, DateTime createdAfter, CancellationToken cancellationToken = default)
    {
        if (!gateway.IsConfigured) return 0;

        var pending = await PaymentQuery()
            .Where(x => x.Status == PaymentStatus.Pending && x.CreatedAt <= createdBefore && x.CreatedAt >= createdAfter &&
                        x.StripePaymentIntentId.StartsWith("pi_"))
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var applied = 0;
        foreach (var payment in pending)
        {
            try
            {
                var intent = await gateway.GetPaymentIntentAsync(payment.StripePaymentIntentId, cancellationToken);
                if (intent.Status == "succeeded")
                {
                    if (await ApplySuccessAsync(payment, intent, cancellationToken))
                    {
                        applied++;
                        logger.LogWarning("Payment {PaymentId} succeeded on Stripe without confirm/webhook; processed by reconciliation.", payment.Id);
                    }
                }
                else if (intent.Status == "canceled")
                {
                    payment.Status = PaymentStatus.Failed;
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
            catch (ValidationException ex)
            {
                logger.LogWarning("Reconciliation of payment {PaymentId} skipped: {Reason}", payment.Id, ex.Message);
            }
        }
        return applied;
    }

    /// <summary>
    /// Idempotency-Key za kreiranje PaymentIntent-a. Isti pokušaj iste pretplate uvijek daje isti ključ (ponovljen ili
    /// istovremen zahtjev vraća isti PaymentIntent), a vrijeme kreiranja pretplate (100 ns) čini ključ jedinstvenim i
    /// izvan ove baze: Stripe ključeve pamti 24 h po Stripe nalogu, a Id pretplate se ponavlja nakon resetovanja baze
    /// ili na drugoj instalaciji sa istim Stripe ključevima. Bez toga bi Stripe odbio zahtjev (idempotency_error)
    /// ili vratio tuđi, možda već plaćeni PaymentIntent.
    /// </summary>
    public static string CreateIntentIdempotencyKey(Subscription subscription, PaymentPurpose purpose, int attempt, long amountMinor) =>
        string.Create(CultureInfo.InvariantCulture,
            $"create-intent:{subscription.Id}:{subscription.CreatedAt:yyyyMMdd'T'HHmmssfffffff}:{purpose}:{attempt}:{amountMinor}");

    /// <summary>Provjerava da Stripe PaymentIntent pripada ovoj uplati (metadata, iznos i valuta). Vraća opis razlike ili null.</summary>
    public static string? FindMismatch(PaymentIntentInfo intent, Payment payment)
    {
        if (!string.Equals(intent.Id, payment.StripePaymentIntentId, StringComparison.Ordinal))
            return "PaymentIntent id";
        if (!intent.Metadata.TryGetValue(PaymentIntentInfo.MetadataSubscriptionId, out var subscriptionId) ||
            subscriptionId != payment.SubscriptionId.ToString(CultureInfo.InvariantCulture))
            return "metadata.subscriptionId";
        if (!intent.Metadata.TryGetValue(PaymentIntentInfo.MetadataPurpose, out var purpose) ||
            !string.Equals(purpose, payment.Purpose.ToString(), StringComparison.Ordinal))
            return "metadata.purpose";
        if (intent.AmountMinor != StripePaymentGateway.ToMinorUnits(payment.Amount))
            return "amount";
        if (!string.Equals(intent.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
            return "currency";
        return null;
    }

    /// <summary>
    /// Provjeri PaymentIntent, pa atomski "preuzmi" uplatu (Pending/Failed → Succeeded samo jednom), tako da
    /// confirm, webhook i usklađivanje koji stignu istovremeno ne mogu dvaput primijeniti istu uplatu.
    /// Vraća false ako je uplatu već obradio neko drugi.
    /// </summary>
    private async Task<bool> ApplySuccessAsync(Payment payment, PaymentIntentInfo intent, CancellationToken cancellationToken)
    {
        if (FindMismatch(intent, payment) is { } field)
        {
            logger.LogError("PaymentIntent {IntentId} does not match payment {PaymentId} ({Field}); payment not applied.",
                intent.Id, payment.Id, field);
            throw new ValidationException(IntentMismatch);
        }

        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var claimed = await db.Payments
            .Where(x => x.Id == payment.Id && (x.Status == PaymentStatus.Pending || x.Status == PaymentStatus.Failed))
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.Status, PaymentStatus.Succeeded).SetProperty(p => p.PaidAt, now), cancellationToken);
        if (claimed == 0) return false;

        await workflow.ApplySuccessfulPaymentAsync(payment, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
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
