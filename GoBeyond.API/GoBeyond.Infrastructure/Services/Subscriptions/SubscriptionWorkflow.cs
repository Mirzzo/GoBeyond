using System.Globalization;
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
/// a pozivalac snima promjene (jedna transakcija).
/// Očekuje učitane: Subscription.ClientProfile.User, Subscription.MentorProfile.User (i Payments gdje je navedeno).
/// </summary>
public interface ISubscriptionWorkflow
{
    int PeriodDays { get; }

    /// <summary>AwaitingMentor → Active (idempotentno ako je već Active).</summary>
    void Accept(Subscription subscription, DateTime now);

    /// <summary>AwaitingMentor → Rejected uz povrat uplate preko Stripe-a.</summary>
    Task RejectAsync(Subscription subscription, string reason, DateTime now, CancellationToken cancellationToken);

    void Cancel(Subscription subscription, string reason, DateTime now, bool notifyClient, bool notifyMentor);

    void Expire(Subscription subscription, DateTime now);

    /// <summary>Označava uplatu uspješnom i primjenjuje posljedice (idempotentno). Vraća false ako je već bila obrađena.</summary>
    bool ApplySuccessfulPayment(Payment payment, DateTime now);

    /// <summary>Povrat novca; baca grešku ako Stripe odbije povrat.</summary>
    Task RefundAsync(Payment payment, DateTime now, CancellationToken cancellationToken);
}

public sealed class SubscriptionWorkflow(
    INotificationSender notifications,
    IPaymentGateway paymentGateway,
    IOptions<LifecycleOptions> lifecycleOptions,
    ILogger<SubscriptionWorkflow> logger) : ISubscriptionWorkflow
{
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
            $"Mentor {mentor.FullName} je prihvatio vaš zahtjev za saradnju. Saradnja traje do {DomainTexts.Date(subscription.EndDate)}, a trening plan ćete dobiti uskoro.",
            sendEmail: true);
    }

    public async Task RejectAsync(Subscription subscription, string reason, DateTime now, CancellationToken cancellationToken)
    {
        if (subscription.Status != SubscriptionStatus.AwaitingMentor)
            throw new ValidationException("Zahtjev se može odbiti samo dok čeka odgovor mentora.");

        foreach (var payment in subscription.Payments.Where(x => x.Status == PaymentStatus.Succeeded))
            await RefundAsync(payment, now, cancellationToken);

        subscription.Status = SubscriptionStatus.Rejected;
        subscription.StatusReason = reason;

        var mentor = subscription.MentorProfile.User;
        notifications.Notify(subscription.ClientProfile.User, NotificationType.RequestRejected,
            "Zahtjev za saradnju je odbijen",
            $"Mentor {mentor.FullName} je odbio vaš zahtjev. Razlog: {reason} Uplaćeni iznos od {Money(subscription.Price, subscription.Currency)} biće vraćen na vašu karticu.",
            sendEmail: true);
    }

    public void Cancel(Subscription subscription, string reason, DateTime now, bool notifyClient, bool notifyMentor)
    {
        if (!QueryExtensions.OpenStatuses.Contains(subscription.Status))
            throw new ValidationException($"Pretplata u statusu \"{DomainTexts.SubscriptionStatusName(subscription.Status)}\" se ne može otkazati.");

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.CancelledAt = now;
        subscription.StatusReason = reason;

        var client = subscription.ClientProfile.User;
        var mentor = subscription.MentorProfile.User;
        if (notifyClient)
            notifications.Notify(client, NotificationType.SubscriptionCancelled, "Saradnja je prekinuta",
                $"Saradnja sa mentorom {mentor.FullName} je prekinuta. Razlog: {reason}", sendEmail: true);
        if (notifyMentor)
            notifications.Notify(mentor, NotificationType.SubscriptionCancelled, "Saradnja je prekinuta",
                $"Saradnja sa klijentom {client.FullName} je prekinuta. Razlog: {reason}", sendEmail: true);
    }

    public void Expire(Subscription subscription, DateTime now)
    {
        subscription.Status = SubscriptionStatus.Expired;
        subscription.StatusReason = DomainTexts.SubscriptionExpiredReason;

        var client = subscription.ClientProfile.User;
        var mentor = subscription.MentorProfile.User;
        notifications.Notify(client, NotificationType.SubscriptionExpired, "Saradnja je istekla",
            $"Pretplata kod mentora {mentor.FullName} je istekla {DomainTexts.Date(subscription.EndDate)}. Vaš plan ostaje dostupan u historiji, a saradnju možete obnoviti novom pretplatom.",
            sendEmail: true);
        notifications.Notify(mentor, NotificationType.SubscriptionExpired, "Saradnja je istekla",
            $"Pretplata klijenta {client.FullName} je istekla {DomainTexts.Date(subscription.EndDate)}.",
            sendEmail: true);
    }

    public bool ApplySuccessfulPayment(Payment payment, DateTime now)
    {
        if (payment.Status == PaymentStatus.Succeeded) return false;

        payment.Status = PaymentStatus.Succeeded;
        payment.PaidAt = now;

        var subscription = payment.Subscription;
        var client = subscription.ClientProfile.User;
        var mentor = subscription.MentorProfile.User;

        if (payment.Purpose == PaymentPurpose.Initial && subscription.Status == SubscriptionStatus.PendingPayment)
        {
            subscription.Status = SubscriptionStatus.AwaitingMentor;
            subscription.PaidAt = now;
            notifications.Notify(mentor, NotificationType.NewCollaborationRequest, "Novi zahtjev za saradnju",
                $"Klijent {client.FullName} je uplatio pretplatu i čeka vaš odgovor. Pregledajte zahtjev u sekciji \"Zahtjevi za saradnju\".",
                sendEmail: true);
            notifications.Notify(client, NotificationType.PaymentSucceeded, "Plaćanje je uspješno",
                $"Uplata od {Money(payment.Amount, payment.Currency)} je uspješna. Mentor {mentor.FullName} će uskoro pregledati vaš zahtjev.",
                sendEmail: true);
        }
        else if (payment.Purpose == PaymentPurpose.Renewal && subscription.Status == SubscriptionStatus.Active)
        {
            var from = subscription.EndDate is { } end && end > now ? end : now;
            subscription.EndDate = from.AddDays(PeriodDays);
            subscription.ExpiryReminderSentAt = null;
            notifications.Notify(client, NotificationType.PaymentSucceeded, "Pretplata je produžena",
                $"Uplata od {Money(payment.Amount, payment.Currency)} je uspješna. Saradnja sa mentorom {mentor.FullName} traje do {DomainTexts.Date(subscription.EndDate)}.",
                sendEmail: true);
            notifications.Notify(mentor, NotificationType.PaymentSucceeded, "Klijent je produžio pretplatu",
                $"Klijent {client.FullName} je produžio saradnju do {DomainTexts.Date(subscription.EndDate)}.",
                sendEmail: false);
        }
        else
        {
            logger.LogWarning("Payment {PaymentId} succeeded while subscription {SubscriptionId} is {Status}.",
                payment.Id, subscription.Id, subscription.Status);
        }
        return true;
    }

    public async Task RefundAsync(Payment payment, DateTime now, CancellationToken cancellationToken)
    {
        if (payment.Status != PaymentStatus.Succeeded) return;

        // Pravi Stripe PaymentIntent ("pi_...") se vraća preko Stripe API-ja. Seed (demo) uplate
        // nikad nisu naplaćene preko Stripe-a ("seed_pi_..."), pa se samo evidentira povrat.
        if (payment.StripePaymentIntentId.StartsWith("pi_", StringComparison.Ordinal))
            await paymentGateway.RefundAsync(payment.StripePaymentIntentId, cancellationToken);

        payment.Status = PaymentStatus.Refunded;
        payment.RefundedAt = now;
    }

    private static string Money(decimal amount, string currency) =>
        $"{amount.ToString("0.00", CultureInfo.InvariantCulture)} {currency.ToUpperInvariant()}";
}
