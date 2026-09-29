using System.Globalization;
using System.Text.Json;
using GoBeyond.Core.DTOs.Subscriptions;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoBeyond.Infrastructure.Services.Payments;

public interface IPaymentService
{
    Task<PaymentIntentDto> CreateIntentAsync(int clientUserId, CreatePaymentIntentRequest request, CancellationToken cancellationToken = default);
    Task<SubscriptionDetailDto> ConfirmAsync(int clientUserId, int paymentId, CancellationToken cancellationToken = default);
    Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default);

    /// <summary>
    /// Usklađivanje sa Stripe-om bez webhook-a: Pending i Failed uplate kreirane između <paramref name="createdAfter"/> i
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
    IOptions<LifecycleOptions> lifecycleOptions,
    ILogger<PaymentService> logger) : IPaymentService
{
    public const string NotConfigured = "Stripe plaćanje nije konfigurisano na serveru.";
    public const string NotCompleted = "Plaćanje nije završeno. Pokušajte ponovo.";
    public const string StillProcessing = "Prethodno plaćanje se još obrađuje. Pokušajte ponovo za nekoliko trenutaka.";
    public const string AlreadyPaid = "Ova uplata je već uspješno izvršena. Osvježite prikaz pretplate.";
    public const string IntentMismatch = "Podaci o plaćanju na Stripe-u ne odgovaraju ovoj uplati, pa plaćanje nije primijenjeno. Kontaktirajte podršku.";
    public const string MentorUnavailable = "Mentor trenutno nije dostupan, pa plaćanje nije moguće.";
    public const string InvalidWebhookPayload = "Neispravan Stripe webhook payload.";

    /// <summary>Stripe statusi u kojima klijent može (ponovo) pokušati plaćanje istim PaymentIntent-om.</summary>
    public static readonly string[] ReusableIntentStatuses = ["requires_payment_method", "requires_confirmation", "requires_action"];

    public async Task<PaymentIntentDto> CreateIntentAsync(int clientUserId, CreatePaymentIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!gateway.IsConfigured) throw new ValidationException(NotConfigured);

        // Pretplata je zaključana do kraja zahtjeva: istovremeni create-intent (dvostruki tap), confirm, otkazivanje i istek
        // iste pretplate čekaju jedan na drugi, pa ponovljen zahtjev dobija isti PaymentIntent umjesto da kreira novi.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockSubscriptionAsync(request.SubscriptionId, cancellationToken);
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
        if (mentor.User.IsDeleted || !mentor.User.IsActive || mentor.User.Role != UserRole.Mentor ||
            mentor.Status != MentorApprovalStatus.Approved)
            throw new ValidationException(MentorUnavailable);

        var amountMinor = StripePaymentGateway.ToMinorUnits(subscription.Price);
        // PaymentIntent se ponovo koristi samo dok je mlađi od pola prozora usklađivanja: i plaćanje nakon ponovnog otvaranja
        // tada stiže unutar prozora, pa se naplata bez potvrde i dalje primijeni ili automatski vrati.
        var reusableSince = DateTime.UtcNow.AddHours(-lifecycleOptions.Value.PaymentReconcileWindowHours / 2.0);

        // Nedovršena uplata istog tipa: ponovo se koristi isti PaymentIntent dok je to moguće.
        foreach (var pending in subscription.Payments
                     .Where(x => x.Status == PaymentStatus.Pending && x.Purpose == purpose)
                     .OrderByDescending(x => x.CreatedAt).ToList())
        {
            var existing = await gateway.GetPaymentIntentAsync(pending.StripePaymentIntentId, cancellationToken);
            var reusable = ReusableIntentStatuses.Contains(existing.Status);

            if (reusable && existing.AmountMinor == amountMinor && pending.CreatedAt >= reusableSince)
            {
                await SaveAndCommitAsync(transaction, cancellationToken);
                return ToDto(pending, existing.ClientSecret);
            }

            if (reusable)
            {
                // Zastario iznos (mentor promijenio cijenu) ili prestar PaymentIntent: otkazuje se na Stripe-u prije nego uplata
                // postane Failed, inače bi ga stari PaymentSheet (drugi uređaj) i dalje mogao naplatiti bez primjene i povrata.
                existing = await gateway.CancelPaymentIntentAsync(existing.Id, SubscriptionWorkflow.CancelIntentIdempotencyKey(pending),
                    cancellationToken);
            }

            if (existing.Status == "succeeded")
            {
                // Uplata je prošla (confirm/webhook još nije stigao ili je klijent upravo platio stari sheet) - primijeni je sada.
                await ApplySuccessAsync(pending, existing, cancellationToken);
                await SaveAndCommitAsync(transaction, cancellationToken);
                throw new ConflictException(AlreadyPaid);
            }

            if (existing.Status == "canceled")
            {
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
            await SaveAndCommitAsync(transaction, cancellationToken);
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

        // Succeeded / Refunded / RefundPending / Disputed su završna stanja - confirm je idempotentan.
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

        if (ReadWebhookEvent(payload) is not { } webhookEvent || string.IsNullOrEmpty(webhookEvent.Intent.Id))
        {
            logger.LogWarning("Signed Stripe webhook without a PaymentIntent object ignored.");
            return;
        }
        var (type, intent) = webhookEvent;

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
    /// PaymentIntent postaje Failed. Provjeravaju se i Failed uplate: zamijenjen PaymentIntent koji je ipak plaćen se
    /// primjenjuje ili vraća, a onaj koji se još može platiti otkazuje (isto i za Pending uplatu pretplate koja je više ne
    /// prima, npr. istekla dok Stripe nije bio dostupan). Greška jedne uplate ne zaustavlja ostale; kad Stripe uopšte nije
    /// dostupan (prekid veze ili timeout), preostale uplate se provjeravaju u sljedećem ciklusu.
    /// </summary>
    public async Task<int> ReconcilePendingAsync(DateTime createdBefore, DateTime createdAfter, CancellationToken cancellationToken = default)
    {
        if (!gateway.IsConfigured) return 0;

        var candidates = await db.Payments.AsNoTracking()
            .Where(x => (x.Status == PaymentStatus.Pending || x.Status == PaymentStatus.Failed) &&
                        x.CreatedAt <= createdBefore && x.CreatedAt >= createdAfter && x.StripePaymentIntentId.StartsWith("pi_"))
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var applied = 0;
        for (var index = 0; index < candidates.Count; index++)
        {
            var paymentId = candidates[index];
            try
            {
                if (await ReconcileAsync(paymentId, cancellationToken)) applied++;
            }
            catch (ValidationException ex) when (ex.Message == StripePaymentGateway.CommunicationFailed)
            {
                // Stripe nije dostupan (prekid veze ili timeout): ostale uplate bi samo čekale isti timeout i odgodile istek i
                // povrate u ovom ciklusu, pa se provjeravaju u sljedećem.
                logger.LogWarning("Stripe is unreachable; reconciliation of payment {PaymentId} and {Remaining} more is postponed to the next run.",
                    paymentId, candidates.Count - index - 1);
                db.ChangeTracker.Clear();
                break;
            }
            catch (DomainException ex)
            {
                logger.LogWarning("Reconciliation of payment {PaymentId} skipped: {Reason}", paymentId, ex.Message);
                db.ChangeTracker.Clear();
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Reconciliation of payment {PaymentId} failed; it will be retried in the next run.", paymentId);
                db.ChangeTracker.Clear();
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

    private async Task<bool> ReconcileAsync(int paymentId, CancellationToken cancellationToken)
    {
        var payment = await PaymentQuery().FirstAsync(x => x.Id == paymentId, cancellationToken);
        if (payment.Status is not (PaymentStatus.Pending or PaymentStatus.Failed)) return false;

        var intent = await gateway.GetPaymentIntentAsync(payment.StripePaymentIntentId, cancellationToken);
        if (ReusableIntentStatuses.Contains(intent.Status) &&
            (payment.Status == PaymentStatus.Failed || !AcceptsPayment(payment.Subscription, payment.Purpose)))
        {
            // Zamijenjen PaymentIntent (npr. iz vremena prije otkazivanja na Stripe-u), ili PaymentIntent pretplate koja ovu
            // uplatu više ne prima (otkazana, istekla, već plaćena...), ne smije se više moći platiti.
            intent = await gateway.CancelPaymentIntentAsync(intent.Id, SubscriptionWorkflow.CancelIntentIdempotencyKey(payment), cancellationToken);
        }

        if (intent.Status == "succeeded")
        {
            if (!await ApplySuccessAsync(payment, intent, cancellationToken)) return false;
            logger.LogWarning("Payment {PaymentId} succeeded on Stripe without confirm/webhook; processed by reconciliation.", payment.Id);
            return true;
        }

        if (intent.Status == "canceled" && payment.Status == PaymentStatus.Pending)
        {
            payment.Status = PaymentStatus.Failed;
            await db.SaveChangesAsync(cancellationToken);
        }
        return false;
    }

    /// <summary>Da li pretplata u trenutnom statusu još prima uplatu ove namjene (početna samo PendingPayment, produženje samo Active).</summary>
    private static bool AcceptsPayment(Subscription subscription, PaymentPurpose purpose) => purpose switch
    {
        PaymentPurpose.Initial => subscription.Status == SubscriptionStatus.PendingPayment,
        PaymentPurpose.Renewal => subscription.Status == SubscriptionStatus.Active,
        _ => false
    };

    /// <summary>
    /// Provjeri PaymentIntent, zaključaj pretplatu, pa atomski "preuzmi" uplatu (Pending/Failed → Succeeded samo jednom), tako
    /// da confirm, webhook i usklađivanje koji stignu istovremeno ne mogu dvaput primijeniti istu uplatu. Odluka (primijeniti
    /// ili vratiti) se donosi nad stanjem pretplate nakon zaključavanja, a ne nad onim pročitanim prije Stripe poziva, pa
    /// istovremeno otkazivanje ili istek ne mogu biti poništeni. Radi u postojećoj transakciji pozivaoca ili otvara svoju.
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

        var ownTransaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        await using (ownTransaction)
        {
            await db.LockSubscriptionAsync(payment.SubscriptionId, cancellationToken);
            await db.Entry(payment.Subscription).ReloadAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var paidAt = intent.ChargedAt ?? now;
            var claimed = await db.Payments
                .Where(x => x.Id == payment.Id && (x.Status == PaymentStatus.Pending || x.Status == PaymentStatus.Failed))
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.Status, PaymentStatus.Succeeded).SetProperty(p => p.PaidAt, paidAt), cancellationToken);
            if (claimed == 0) return false;

            payment.PaidAt = paidAt;
            await workflow.ApplySuccessfulPaymentAsync(payment, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            if (ownTransaction is not null) await ownTransaction.CommitAsync(cancellationToken);
            return true;
        }
    }

    /// <summary>
    /// Tip događaja i PaymentIntent iz webhook payload-a. Potpisan payload koji nije JSON → 400; JSON neočekivanog oblika
    /// (bez objekta data.object) → null, pa se događaj ignoriše (200) umjesto 500.
    /// </summary>
    private static (string? Type, PaymentIntentInfo Intent)? ReadWebhookEvent(string payload)
    {
        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            throw new ValidationException(InvalidWebhookPayload);
        }

        using (json)
        {
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("object", out var obj) || obj.ValueKind != JsonValueKind.Object)
                return null;

            var type = root.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String
                ? typeElement.GetString()
                : null;
            return (type, PaymentIntentInfo.FromJson(obj));
        }
    }

    private async Task SaveAndCommitAsync(IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
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
