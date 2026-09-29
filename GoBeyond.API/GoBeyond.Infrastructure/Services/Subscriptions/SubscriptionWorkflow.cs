using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Services.Notifications;
using GoBeyond.Infrastructure.Services.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoBeyond.Infrastructure.Services.Subscriptions;

/// <summary>
/// Prijelazi statusa pretplate na jednom mjestu (koriste ga mentor, klijent, admin,
/// plaćanja i SubscriptionLifecycleService). Metode mijenjaju entitete i dodaju obavijesti,
/// a pozivalac snima promjene (jedna transakcija, nad pretplatom zaključanom sa <see cref="SubscriptionLocks"/>).
/// Očekuje učitane: Subscription.ClientProfile.User, Subscription.MentorProfile.User (i Payments gdje je navedeno).
/// </summary>
public interface ISubscriptionWorkflow
{
    int PeriodDays { get; }

    /// <summary>AwaitingMentor → Active (idempotentno ako je već Active).</summary>
    void Accept(Subscription subscription, DateTime now);

    /// <summary>
    /// AwaitingMentor → Rejected uz povrat uplata preko Stripe-a (ako povrat ne uspije, odbijanje se ne izvršava). Osporena
    /// naplata se ne vraća, nego dobija status Disputed (vidi <see cref="RefundOrFailAsync"/>). Vraća iznose koje je ovo
    /// odbijanje vratilo, odnosno ostavilo osporenim.
    /// </summary>
    Task<RefundOutcome> RejectAsync(Subscription subscription, string reason, DateTime now, CancellationToken cancellationToken);

    /// <summary>Samo prelaz u Cancelled i obavijesti, bez povrata; otkazivanje pretplate sa uplatama ide kroz <see cref="CancelAsync"/>.</summary>
    void Cancel(Subscription subscription, string reason, DateTime now, bool notifyClient, bool notifyMentor);

    /// <summary>
    /// Otkazivanje (klijent, administrator, brisanje korisnika). Plaćen zahtjev koji mentor nije prihvatio (AwaitingMentor) se
    /// vraća klijentu, a tekst obavijesti navodi vraćeni (i osporeni) iznos; ako povrat ne uspije, baca ValidationException
    /// sa <paramref name="refundFailedMessage"/> i pozivalac ništa ne snima. Active i PendingPayment ostaju bez povrata.
    /// Nedovršene uplate se zatvaraju (<see cref="SettlePendingPaymentsAsync"/>). Mentor dobija obavijest samo ako je zahtjev
    /// vidio (pretplata nije bila PendingPayment). Očekuje učitane Payments.
    /// </summary>
    Task CancelAsync(Subscription subscription, string reason, DateTime now, bool notifyClient, bool notifyMentor,
        string refundFailedMessage, CancellationToken cancellationToken);

    void Expire(Subscription subscription, DateTime now);

    /// <summary>
    /// Označava uplatu uspješnom i primjenjuje je na pretplatu (početna uplata postavlja i cijenu pretplate na naplaćeni
    /// iznos). Ako se uplata ne može primijeniti (pretplata je u međuvremenu otkazana/istekla ili je uplata duplikat), novac
    /// se automatski vraća.
    /// Vraća false ako je uplata već bila obrađena. Payment.PaidAt (vrijeme naplate na Stripe-u) se zadržava ako je postavljen.
    /// </summary>
    Task<bool> ApplySuccessfulPaymentAsync(Payment payment, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// Zatvara nedovršene (Pending) uplate pretplate koja prestaje primati uplate (otkazivanje, istek): naplaćena uplata se
    /// primjenjuje ako može (npr. produženje plaćeno prije isteka) ili se automatski vraća, a PaymentIntent koji se još može
    /// platiti se otkazuje na Stripe-u (uplata Failed). Ako <paramref name="stripeErrorsAreFatal"/> nije postavljen, greška
    /// Stripe-a se samo loguje i uplata ostaje Pending za usklađivanje. Očekuje učitane Payments. Vraća false ako Stripe
    /// nije bio dostupan (prekid veze ili timeout); preostale uplate se tada ne provjeravaju.
    /// </summary>
    Task<bool> SettlePendingPaymentsAsync(Subscription subscription, DateTime now, bool stripeErrorsAreFatal, CancellationToken cancellationToken);

    /// <summary>
    /// Povrat novca; baca ValidationException sa zadanom porukom ako Stripe odbije povrat. Naplatu osporenu kod banke
    /// klijenta Stripe ne vraća, pa uplata tada dobija status Disputed (bez izuzetka) i pozivalac nastavlja.
    /// </summary>
    Task RefundOrFailAsync(Payment payment, DateTime now, string failureMessage, CancellationToken cancellationToken);

    /// <summary>
    /// Ponovni pokušaj povrata za uplatu u statusu RefundPending. Vraća true ako je povrat uspio. Osporena naplata prelazi u
    /// Disputed (klijent dobija obavijest) i više se ne pokušava; ostale greške Stripe-a ostavljaju RefundPending.
    /// </summary>
    Task<bool> RetryPendingRefundAsync(Payment payment, DateTime now, CancellationToken cancellationToken);
}

public sealed class SubscriptionWorkflow(
    INotificationSender notifications,
    IPaymentGateway paymentGateway,
    IOptions<LifecycleOptions> lifecycleOptions,
    ILogger<SubscriptionWorkflow> logger) : ISubscriptionWorkflow
{
    public const string RejectRefundFailed = "Odbijanje nije moguće: povrat uplate klijentu nije uspio. Pokušajte ponovo.";

    private const string DisputedTitle = "Uplata je osporena";
    private const string DisputeOutcomeSentence = " O povratu odlučuje postupak osporavanja.";

    private static readonly PaymentStatus[] RefundableStatuses = [PaymentStatus.Succeeded, PaymentStatus.RefundPending];

    public int PeriodDays => lifecycleOptions.Value.SubscriptionPeriodDays;

    public void Accept(Subscription subscription, DateTime now)
    {
        if (subscription.Status == SubscriptionStatus.Active) return;
        if (subscription.Status != SubscriptionStatus.AwaitingMentor)
            throw new ValidationException("Zahtjev se može prihvatiti samo dok čeka odgovor mentora.");

        subscription.Status = SubscriptionStatus.Active;
        subscription.AcceptedAt = now;
        subscription.StartDate = now;
        subscription.EndDate = now.AddDays(PeriodDays);
        subscription.StatusReason = null;

        var mentor = subscription.MentorProfile.User;
        notifications.Notify(subscription.ClientProfile.User, NotificationType.RequestAccepted,
            "Zahtjev za saradnju je prihvaćen",
            $"Vaš zahtjev za saradnju sa mentorom {mentor.FullName} je prihvaćen. Saradnja traje do {Date(subscription.EndDate)}, a trening plan ćete dobiti uskoro.",
            sendEmail: true);
    }

    public async Task<RefundOutcome> RejectAsync(Subscription subscription, string reason, DateTime now, CancellationToken cancellationToken)
    {
        if (subscription.Status != SubscriptionStatus.AwaitingMentor)
            throw new ValidationException("Zahtjev se može odbiti samo dok čeka odgovor mentora.");

        var refunds = await RefundAllAsync(subscription, now, RejectRefundFailed, cancellationToken);

        subscription.Status = SubscriptionStatus.Rejected;
        subscription.StatusReason = reason;

        var mentor = subscription.MentorProfile.User;
        notifications.Notify(subscription.ClientProfile.User, NotificationType.RequestRejected,
            "Zahtjev za saradnju je odbijen",
            $"Vaš zahtjev za saradnju sa mentorom {mentor.FullName} je odbijen. Razlog: {DomainTexts.Sentence(reason)}{RefundSentences(refunds, subscription.Currency)}",
            sendEmail: true);
        return refunds;
    }

    public void Cancel(Subscription subscription, string reason, DateTime now, bool notifyClient, bool notifyMentor)
    {
        EnsureCancellable(subscription);
        CancelAndNotify(subscription, reason, now, notifyClient, notifyMentor, refunds: default);
    }

    public async Task CancelAsync(Subscription subscription, string reason, DateTime now, bool notifyClient, bool notifyMentor,
        string refundFailedMessage, CancellationToken cancellationToken)
    {
        EnsureCancellable(subscription);

        // Zahtjev koji mentor nije prihvatio: usluga nije pružena, pa se uplata vraća (kao kod odbijanja zahtjeva).
        var refunds = subscription.Status == SubscriptionStatus.AwaitingMentor
            ? await RefundAllAsync(subscription, now, refundFailedMessage, cancellationToken)
            : default;
        CancelAndNotify(subscription, reason, now, notifyClient, notifyMentor, refunds);

        // Nakon prelaza u Cancelled: naplaćena nedovršena uplata se vraća, a plativ PaymentIntent otkazuje.
        await SettlePendingPaymentsAsync(subscription, now, stripeErrorsAreFatal: true, cancellationToken);
    }

    public void Expire(Subscription subscription, DateTime now)
    {
        subscription.Status = SubscriptionStatus.Expired;
        subscription.StatusReason = DomainTexts.SubscriptionExpiredReason;

        var client = subscription.ClientProfile.User;
        var mentor = subscription.MentorProfile.User;
        notifications.Notify(client, NotificationType.SubscriptionExpired, "Saradnja je istekla",
            $"Pretplata kod mentora {mentor.FullName} je istekla {Date(subscription.EndDate)} Vaš plan ostaje dostupan u historiji, a saradnju možete obnoviti novom pretplatom.",
            sendEmail: true);
        notifications.Notify(mentor, NotificationType.SubscriptionExpired, "Saradnja je istekla",
            $"Pretplata klijenta {client.FullName} je istekla {Date(subscription.EndDate)}",
            sendEmail: true);
    }

    public async Task<bool> ApplySuccessfulPaymentAsync(Payment payment, DateTime now, CancellationToken cancellationToken)
    {
        if (payment.Status is PaymentStatus.Succeeded or PaymentStatus.Refunded or PaymentStatus.RefundPending or PaymentStatus.Disputed)
            return false;

        payment.Status = PaymentStatus.Succeeded;
        payment.PaidAt ??= now;

        var subscription = payment.Subscription;
        var client = subscription.ClientProfile.User;
        var mentor = subscription.MentorProfile.User;

        if (payment.Purpose == PaymentPurpose.Initial && subscription.Status == SubscriptionStatus.PendingPayment)
        {
            // Cijena pretplate je naplaćeni iznos: klijent je mogao platiti raniji PaymentIntent nakon što je ponovni ulazak
            // u plaćanje (POST /api/subscriptions) pretplati postavio novu cijenu mentora.
            subscription.Price = payment.Amount;
            subscription.Currency = payment.Currency;
            subscription.Status = SubscriptionStatus.AwaitingMentor;
            subscription.PaidAt = payment.PaidAt;
            notifications.Notify(mentor, NotificationType.NewCollaborationRequest, "Novi zahtjev za saradnju",
                $"Uplata klijenta {client.FullName} je uspješna i zahtjev za saradnju čeka vaš odgovor. Pregledajte ga u sekciji \"Zahtjevi za saradnju\".",
                sendEmail: true);
            notifications.Notify(client, NotificationType.PaymentSucceeded, "Plaćanje je uspješno",
                $"Uplata od {Money(payment.Amount, payment.Currency)} je uspješna. Mentor {mentor.FullName} će uskoro pregledati vaš zahtjev.",
                sendEmail: true);
        }
        else if (payment.Purpose == PaymentPurpose.Renewal && subscription.Status == SubscriptionStatus.Active)
        {
            // Produženje plaćeno prije isteka se nastavlja na postojeći kraj, i kad je primijenjeno tek nakon njega.
            var from = subscription.EndDate is { } end && end > payment.PaidAt ? end : now;
            subscription.EndDate = from.AddDays(PeriodDays);
            subscription.ExpiryReminderSentAt = null;
            notifications.Notify(client, NotificationType.PaymentSucceeded, "Pretplata je produžena",
                $"Uplata od {Money(payment.Amount, payment.Currency)} je uspješna. Saradnja sa mentorom {mentor.FullName} traje do {Date(subscription.EndDate)}",
                sendEmail: true);
            notifications.Notify(mentor, NotificationType.PaymentSucceeded, "Pretplata je produžena",
                $"Saradnja sa klijentom {client.FullName} je produžena do {Date(subscription.EndDate)}",
                sendEmail: false);
        }
        else
        {
            await RefundUnappliedPaymentAsync(payment, now, cancellationToken);
        }
        return true;
    }

    public async Task<bool> SettlePendingPaymentsAsync(Subscription subscription, DateTime now, bool stripeErrorsAreFatal,
        CancellationToken cancellationToken)
    {
        // Seed (demo) uplate nikad nisu išle preko Stripe-a, a bez ključeva se Stripe ne može pitati.
        var pending = subscription.Payments
            .Where(x => x.Status == PaymentStatus.Pending && x.StripePaymentIntentId.StartsWith("pi_", StringComparison.Ordinal))
            .ToList();
        if (pending.Count == 0 || !paymentGateway.IsConfigured) return true;

        foreach (var payment in pending)
        {
            PaymentIntentInfo intent;
            try
            {
                intent = await paymentGateway.GetPaymentIntentAsync(payment.StripePaymentIntentId, cancellationToken);
                if (PaymentService.ReusableIntentStatuses.Contains(intent.Status))
                    intent = await paymentGateway.CancelPaymentIntentAsync(intent.Id, CancelIntentIdempotencyKey(payment), cancellationToken);
            }
            catch (Exception ex) when (!stripeErrorsAreFatal && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Pending payment {PaymentId} was not settled ({Reason}); reconciliation will check it again.",
                    payment.Id, ex.Message);
                if (ex is ValidationException { Message: StripePaymentGateway.CommunicationFailed }) return false;
                continue;
            }

            if (intent.Status == "canceled")
            {
                payment.Status = PaymentStatus.Failed;
            }
            else if (intent.Status == "succeeded")
            {
                if (PaymentService.FindMismatch(intent, payment) is { } field)
                {
                    logger.LogError("PaymentIntent {IntentId} does not match payment {PaymentId} ({Field}); payment not settled.",
                        intent.Id, payment.Id, field);
                    continue;
                }
                // Kartica je naplaćena, a potvrda nije stigla: primjenjuje se (npr. produženje prije isteka) ili automatski vraća.
                payment.PaidAt = intent.ChargedAt;
                await ApplySuccessfulPaymentAsync(payment, now, cancellationToken);
            }
            else
            {
                // processing i sl.: ishod stiže kasnije, a usklađivanje uplatu tada primjenjuje ili vraća.
                logger.LogInformation("Pending payment {PaymentId} is {Status} on Stripe; left for reconciliation.", payment.Id, intent.Status);
            }
        }
        return true;
    }

    public async Task RefundOrFailAsync(Payment payment, DateTime now, string failureMessage, CancellationToken cancellationToken)
    {
        try
        {
            await RefundAsync(payment, now, cancellationToken);
        }
        catch (ValidationException ex)
        {
            logger.LogError("Refund of payment {PaymentId} failed: {Reason}", payment.Id, ex.Message);
            throw new ValidationException(failureMessage);
        }
    }

    public async Task<bool> RetryPendingRefundAsync(Payment payment, DateTime now, CancellationToken cancellationToken)
    {
        if (payment.Status != PaymentStatus.RefundPending) return false;
        try
        {
            await RefundAsync(payment, now, cancellationToken);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning("Pending refund of payment {PaymentId} failed again: {Reason}", payment.Id, ex.Message);
            return false;
        }

        var client = payment.Subscription.ClientProfile.User;
        if (payment.Status == PaymentStatus.Disputed)
        {
            // Klijent je ranije obaviješten da se povrat automatski ponavlja, pa dobija obavijest da se više ne ponavlja.
            notifications.Notify(client, NotificationType.PaymentRefunded, DisputedTitle,
                $"Uplata od {Money(payment.Amount, payment.Currency)} je osporena kod vaše banke, pa se njen povrat više ne " +
                $"ponavlja automatski.{DisputeOutcomeSentence}",
                sendEmail: true);
            return false;
        }

        notifications.Notify(client, NotificationType.PaymentRefunded, "Povrat uplate je izvršen",
            $"Iznos od {Money(payment.Amount, payment.Currency)} je vraćen na vašu karticu.", sendEmail: true);
        return true;
    }

    /// <summary>
    /// Idempotency-Key povrata: "refund:{PaymentIntent id}". Id PaymentIntent-a je jedinstven na Stripe nalogu, pa ključ
    /// važi i nakon resetovanja baze ili na drugoj instalaciji sa istim ključevima (Id uplate iz baze se tada ponavlja,
    /// a Stripe bi isti ključ sa drugim PaymentIntent-om odbio kao idempotency_error).
    /// </summary>
    public static string RefundIdempotencyKey(Payment payment) => $"refund:{payment.StripePaymentIntentId}";

    /// <summary>Idempotency-Key otkazivanja PaymentIntent-a: "cancel:{PaymentIntent id}" (isti razlog kao kod povrata).</summary>
    public static string CancelIntentIdempotencyKey(Payment payment) => $"cancel:{payment.StripePaymentIntentId}";

    private static void EnsureCancellable(Subscription subscription)
    {
        if (!QueryExtensions.OpenStatuses.Contains(subscription.Status))
            throw new ValidationException($"Pretplata u statusu \"{DomainTexts.SubscriptionStatusName(subscription.Status)}\" se ne može otkazati.");
    }

    private void CancelAndNotify(Subscription subscription, string reason, DateTime now, bool notifyClient, bool notifyMentor,
        RefundOutcome refunds)
    {
        // Neplaćenu (PendingPayment) pretplatu mentor nikad nije vidio, pa o njenom prekidu ne dobija obavijest.
        var mentorSawRequest = subscription.Status != SubscriptionStatus.PendingPayment;

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.CancelledAt = now;
        subscription.StatusReason = reason;

        var client = subscription.ClientProfile.User;
        var mentor = subscription.MentorProfile.User;
        if (notifyClient)
            notifications.Notify(client, NotificationType.SubscriptionCancelled, "Saradnja je prekinuta",
                $"Saradnja sa mentorom {mentor.FullName} je prekinuta. Razlog: {DomainTexts.Sentence(reason)}{RefundSentences(refunds, subscription.Currency)}",
                sendEmail: true);
        if (notifyMentor && mentorSawRequest)
            notifications.Notify(mentor, NotificationType.SubscriptionCancelled, "Saradnja je prekinuta",
                $"Saradnja sa klijentom {client.FullName} je prekinuta. Razlog: {DomainTexts.Sentence(reason)}", sendEmail: true);
    }

    /// <summary>
    /// Vraća sve uspješne (i RefundPending) uplate pretplate; rezultat je ukupno vraćeni i ukupno osporeni iznos (za tekst
    /// obavijesti).
    /// </summary>
    private async Task<RefundOutcome> RefundAllAsync(Subscription subscription, DateTime now, string failureMessage,
        CancellationToken cancellationToken)
    {
        RefundOutcome totals = default;
        foreach (var payment in subscription.Payments.Where(x => RefundableStatuses.Contains(x.Status)).ToList())
        {
            await RefundOrFailAsync(payment, now, failureMessage, cancellationToken);
            totals = payment.Status == PaymentStatus.Disputed
                ? totals with { Disputed = totals.Disputed + payment.Amount }
                : totals with { Refunded = totals.Refunded + payment.Amount };
        }
        return totals;
    }

    /// <summary>
    /// Naplaćena uplata koja se ne može primijeniti (pretplata više nije u odgovarajućem statusu, duplikat...)
    /// se odmah vraća. Ako Stripe povrat ne uspije, uplata ostaje u statusu RefundPending (trajna oznaka)
    /// i SubscriptionLifecycleService ponavlja povrat.
    /// </summary>
    private async Task RefundUnappliedPaymentAsync(Payment payment, DateTime now, CancellationToken cancellationToken)
    {
        var subscription = payment.Subscription;
        var client = subscription.ClientProfile.User;
        var mentor = subscription.MentorProfile.User;
        var statusName = DomainTexts.SubscriptionStatusName(subscription.Status);
        logger.LogError("Payment {PaymentId} ({Purpose}) succeeded while subscription {SubscriptionId} is {Status}; refunding it.",
            payment.Id, payment.Purpose, subscription.Id, subscription.Status);

        try
        {
            await RefundAsync(payment, now, cancellationToken);
            var disputed = payment.Status == PaymentStatus.Disputed;
            notifications.Notify(client, NotificationType.PaymentRefunded, disputed ? DisputedTitle : "Uplata je vraćena",
                $"Uplata od {Money(payment.Amount, payment.Currency)} za saradnju sa mentorom {mentor.FullName} nije mogla biti primijenjena " +
                $"jer pretplata više nije u odgovarajućem statusu ({statusName}). " +
                (disputed
                    ? $"Uplata je osporena kod vaše banke, pa se ne vraća automatski.{DisputeOutcomeSentence}"
                    : "Iznos je vraćen na vašu karticu."),
                sendEmail: true);
        }
        catch (ValidationException ex)
        {
            payment.Status = PaymentStatus.RefundPending;
            logger.LogError("Automatic refund of payment {PaymentId} failed ({Reason}); marked RefundPending for retry.", payment.Id, ex.Message);
            notifications.Notify(client, NotificationType.PaymentRefunded, "Povrat uplate je u obradi",
                $"Uplata od {Money(payment.Amount, payment.Currency)} za saradnju sa mentorom {mentor.FullName} nije mogla biti primijenjena " +
                $"jer pretplata više nije u odgovarajućem statusu ({statusName}). Povrat novca je u obradi i biće automatski ponovljen.",
                sendEmail: true);
        }
    }

    /// <summary>
    /// Pravi Stripe PaymentIntent ("pi_...") se vraća preko Stripe Refund API-ja (Idempotency-Key iz
    /// <see cref="RefundIdempotencyKey"/>, pa ponovljen pokušaj ne vraća novac dvaput). Seed (demo) uplate nikad nisu
    /// naplaćene preko Stripe-a ("seed_pi_..."), pa se za njih samo evidentira povrat. Osporenu naplatu Stripe ne vraća:
    /// uplata tada dobija status Disputed, pa je automatski povrat više ne pokušava.
    /// </summary>
    private async Task RefundAsync(Payment payment, DateTime now, CancellationToken cancellationToken)
    {
        if (!RefundableStatuses.Contains(payment.Status)) return;

        if (payment.StripePaymentIntentId.StartsWith("pi_", StringComparison.Ordinal) &&
            await paymentGateway.RefundAsync(payment.StripePaymentIntentId, RefundIdempotencyKey(payment), cancellationToken) ==
            RefundResult.ChargeDisputed)
        {
            logger.LogWarning("Refund of payment {PaymentId} is not possible because the charge is disputed; marked Disputed " +
                              "and left to the dispute in Stripe, with no further automatic retries.", payment.Id);
            payment.Status = PaymentStatus.Disputed;
            return;
        }

        payment.Status = PaymentStatus.Refunded;
        payment.RefundedAt = now;
    }

    /// <summary>Datum u vremenskoj zoni platforme (Lifecycle:TimeZoneId); završava tačkom, pa rečenica ne dodaje drugu.</summary>
    private string Date(DateTime? value) => DomainTexts.Date(value, lifecycleOptions.Value.TimeZoneId);

    private static string RefundSentences(RefundOutcome refunds, string currency) =>
        (refunds.Refunded > 0 ? $" Uplaćeni iznos od {Money(refunds.Refunded, currency)} biće vraćen na vašu karticu." : string.Empty) +
        (refunds.Disputed > 0
            ? $" Uplata od {Money(refunds.Disputed, currency)} je osporena kod vaše banke, pa se ne vraća automatski.{DisputeOutcomeSentence}"
            : string.Empty);

    private static string Money(decimal amount, string currency) => DomainTexts.Money(amount, currency);
}

/// <summary>Ukupno vraćeni i ukupno osporeni (Disputed, nevraćeni) iznos uplata u jednom prelazu pretplate.</summary>
public readonly record struct RefundOutcome(decimal Refunded, decimal Disputed);
