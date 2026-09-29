using System.Linq.Expressions;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Notifications;
using GoBeyond.Infrastructure.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoBeyond.Infrastructure.Services.Subscriptions;

public sealed record LifecycleRunResult(int PaymentsReconciled, int Expired, int ExpiringReminders, int PlanMissingReminders,
    int InactivityReminders, int RefundsCompleted);

public interface ISubscriptionLifecycleProcessor
{
    Task<LifecycleRunResult> RunAsync(DateTime now, CancellationToken cancellationToken = default);
}

/// <summary>
/// Automatske obavijesti o važnim događajima (prijava: "izostanak plana, neaktivnost, istek saradnje").
/// Pokreće ga SubscriptionLifecycleService (hosted servis) u intervalu iz konfiguracije.
/// </summary>
public sealed class SubscriptionLifecycleProcessor(
    GoBeyondDbContext db,
    ISubscriptionWorkflow workflow,
    INotificationSender notifications,
    IPaymentService payments,
    IOptions<LifecycleOptions> options,
    ILogger<SubscriptionLifecycleProcessor> logger) : ISubscriptionLifecycleProcessor
{
    public async Task<LifecycleRunResult> RunAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        // Usklađivanje uplata ide prvo: produženje plaćeno tik prije isteka (a nepotvrđeno) mora produžiti
        // pretplatu prije nego je ExpireAsync proglasi isteklom (inače bi uplata bila vraćena umjesto primijenjena).
        // Uplate mlađe od praga usklađivanja ExpireAsync sam provjerava na Stripe-u prije isteka.
        var result = new LifecycleRunResult(
            await StepAsync("payment reconciliation", () => payments.ReconcilePendingAsync(
                now.AddMinutes(-settings.PaymentReconcileAfterMinutes), now.AddHours(-settings.PaymentReconcileWindowHours), cancellationToken),
                cancellationToken),
            await StepAsync("expiry", () => ExpireAsync(now, cancellationToken), cancellationToken),
            await StepAsync("expiring reminders", () => RemindExpiringAsync(now, settings, cancellationToken), cancellationToken),
            await StepAsync("missing plan reminders", () => RemindMissingPlansAsync(now, settings, cancellationToken), cancellationToken),
            await StepAsync("inactivity reminders", () => RemindInactiveClientsAsync(now, settings, cancellationToken), cancellationToken),
            await StepAsync("refund retries", () => RetryPendingRefundsAsync(now, cancellationToken), cancellationToken));

        if (result != new LifecycleRunResult(0, 0, 0, 0, 0, 0))
            logger.LogInformation("Subscription lifecycle: {Result}", result);
        return result;
    }

    /// <summary>
    /// Greška jednog koraka (npr. prekid veze sa bazom) se loguje, a ostali koraci se ipak izvršavaju; korak se ponavlja u
    /// sljedećem ciklusu.
    /// </summary>
    private async Task<int> StepAsync(string name, Func<Task<int>> step, CancellationToken cancellationToken)
    {
        try
        {
            return await step();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Subscription lifecycle step '{Step}' failed; it will be retried in the next run.", name);
            db.ChangeTracker.Clear();
            return 0;
        }
    }

    /// <summary>
    /// Istek saradnje: Active s prošlim EndDate → Expired, obavijest klijentu i mentoru. Svaka pretplata ide u svojoj
    /// transakciji nad zaključanim redom (istovremeni confirm produženja čeka ili je već primijenjen), a greška jedne ne
    /// zaustavlja ostale.
    /// </summary>
    private async Task<int> ExpireAsync(DateTime now, CancellationToken cancellationToken)
    {
        var candidates = await db.Subscriptions.AsNoTracking()
            .Where(x => x.Status == SubscriptionStatus.Active && x.EndDate != null && x.EndDate < now)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var expired = 0;
        var stripeReachable = true;
        foreach (var subscriptionId in candidates)
        {
            try
            {
                var outcome = await ExpireOneAsync(subscriptionId, now, stripeReachable, cancellationToken);
                if (outcome.Expired) expired++;
                // Nedostupan Stripe (prekid veze ili timeout) se ne pita ponovo u ovom ciklusu: ostale pretplate ističu bez
                // čekanja istog timeout-a, a njihove nepotvrđene uplate provjerava usklađivanje.
                stripeReachable &= outcome.StripeReachable;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Expiry of subscription {SubscriptionId} failed; it will be retried in the next run.", subscriptionId);
                db.ChangeTracker.Clear();
            }
        }
        return expired;
    }

    /// <summary>
    /// Prije isteka se nepotvrđene uplate pretplate provjere na Stripe-u, bez obzira na starost: produženje plaćeno tik prije
    /// isteka (confirm nije stigao) produžava pretplatu umjesto da bude vraćeno, a neplaćen PaymentIntent se otkazuje.
    /// Ako Stripe nije dostupan, pretplata ipak ističe (naplaćeno produženje kasnije vraća usklađivanje).
    /// </summary>
    private async Task<(bool Expired, bool StripeReachable)> ExpireOneAsync(int subscriptionId, DateTime now, bool checkStripe,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockSubscriptionAsync(subscriptionId, cancellationToken);
        var subscription = await SubscriptionsWithUsers().Include(x => x.Payments)
            .FirstAsync(x => x.Id == subscriptionId, cancellationToken);
        if (subscription.Status != SubscriptionStatus.Active || subscription.EndDate is not { } endDate || endDate >= now)
            return (false, checkStripe);

        var stripeReachable = checkStripe &&
                              await workflow.SettlePendingPaymentsAsync(subscription, now, stripeErrorsAreFatal: false, cancellationToken);
        var renewed = subscription.EndDate >= now;
        if (!renewed) workflow.Expire(subscription, now);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (!renewed, stripeReachable);
    }

    /// <summary>Podsjetnik klijentu N dana prije isteka (jednom po periodu; obnova resetuje podsjetnik).</summary>
    private Task<int> RemindExpiringAsync(DateTime now, LifecycleOptions settings, CancellationToken cancellationToken)
    {
        var limit = now.AddDays(settings.ExpiringReminderDays);
        return RemindEachAsync(
            x => x.Status == SubscriptionStatus.Active && x.ExpiryReminderSentAt == null &&
                 x.EndDate != null && x.EndDate > now && x.EndDate <= limit,
            subscription =>
            {
                subscription.ExpiryReminderSentAt = now;
                notifications.Notify(subscription.ClientProfile.User, NotificationType.SubscriptionExpiring, "Pretplata uskoro ističe",
                    $"Saradnja sa mentorom {subscription.MentorProfile.User.FullName} ističe {DomainTexts.Date(subscription.EndDate, settings.TimeZoneId)} " +
                    "Produžite pretplatu u sekciji \"Pretplata\" kako biste zadržali svoj plan.",
                    sendEmail: true);
            },
            cancellationToken);
    }

    /// <summary>Izostanak plana: aktivna saradnja duže od N sati bez objavljenog plana (najviše jednom u M sati).</summary>
    private Task<int> RemindMissingPlansAsync(DateTime now, LifecycleOptions settings, CancellationToken cancellationToken)
    {
        var acceptedBefore = now.AddHours(-settings.PlanMissingAfterHours);
        var repeatBefore = now.AddHours(-settings.PlanMissingRepeatHours);
        return RemindEachAsync(
            x => x.Status == SubscriptionStatus.Active && x.AcceptedAt != null && x.AcceptedAt <= acceptedBefore &&
                 (x.TrainingPlan == null || x.TrainingPlan.Status == TrainingPlanStatus.Draft) &&
                 (x.PlanMissingReminderSentAt == null || x.PlanMissingReminderSentAt <= repeatBefore),
            subscription =>
            {
                subscription.PlanMissingReminderSentAt = now;
                notifications.Notify(subscription.MentorProfile.User, NotificationType.PlanMissing, "Klijent čeka trening plan",
                    $"Saradnja sa klijentom {subscription.ClientProfile.User.FullName} je aktivna od {DomainTexts.Date(subscription.AcceptedAt, settings.TimeZoneId)}, " +
                    "a plan još nije objavljen. Izradite i objavite plan u sekciji \"Zahtjevi za saradnju\".",
                    sendEmail: true);
            },
            cancellationToken);
    }

    /// <summary>
    /// Podsjetnik za svaku pretplatu ide u njenoj transakciji nad zaključanim redom, a uslov se nakon zaključavanja
    /// provjerava ponovo: istovremeno produženje (novi EndDate, resetovan podsjetnik) ili otkazivanje se tako ne pregazi i
    /// ne šalje se zastario datum. Greška jedne pretplate ne zaustavlja ostale; ponavlja se u sljedećem ciklusu.
    /// </summary>
    private async Task<int> RemindEachAsync(Expression<Func<Core.Entities.Subscription, bool>> due,
        Action<Core.Entities.Subscription> remind, CancellationToken cancellationToken)
    {
        var candidates = await db.Subscriptions.AsNoTracking().Where(due).Select(x => x.Id).ToListAsync(cancellationToken);

        var reminded = 0;
        foreach (var subscriptionId in candidates)
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                await db.LockSubscriptionAsync(subscriptionId, cancellationToken);
                var subscription = await SubscriptionsWithUsers().Where(due).FirstOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken);
                if (subscription is null) continue;

                remind(subscription);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                reminded++;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Reminder for subscription {SubscriptionId} failed; it will be retried in the next run.", subscriptionId);
                db.ChangeTracker.Clear();
            }
        }
        return reminded;
    }

    /// <summary>Neaktivnost: klijent bez aktivnosti N dana (najviše jedna obavijest u M dana).</summary>
    private async Task<int> RemindInactiveClientsAsync(DateTime now, LifecycleOptions settings, CancellationToken cancellationToken)
    {
        var inactiveBefore = now.AddDays(-settings.InactivityDays);
        var repeatAfter = now.AddDays(-settings.InactivityRepeatDays);

        var candidates = await db.Users
            .Where(x => x.Role == UserRole.Client && x.IsActive && !x.IsDeleted &&
                        !x.Notifications.Any(n => n.Type == NotificationType.Inactivity && n.CreatedAt >= repeatAfter))
            .Select(x => new
            {
                User = x,
                LastHeartbeat = x.Activities.Max(a => (DateTime?)a.LastHeartbeatAt)
            })
            .ToListAsync(cancellationToken);

        var inactive = candidates
            .Where(x => new[] { x.LastHeartbeat, x.User.LastLoginAt, x.User.CreatedAt }.Max() < inactiveBefore)
            .ToList();

        foreach (var candidate in inactive)
        {
            var days = (int)(now - (new[] { candidate.LastHeartbeat, candidate.User.LastLoginAt, candidate.User.CreatedAt }.Max() ?? now)).TotalDays;
            notifications.Notify(candidate.User, NotificationType.Inactivity, "Nedostajete nam!",
                $"Niste bili aktivni {days} dana. Vratite se svom planu – svaki trening vas vodi korak dalje.",
                sendEmail: true);
        }
        await db.SaveChangesAsync(cancellationToken);
        return inactive.Count;
    }

    /// <summary>
    /// Ponovni pokušaj povrata za uplate koje nisu mogle biti primijenjene (status RefundPending). Neočekivana greška jedne
    /// uplate (npr. Stripe 409 dok je isti povrat još u obradi) ne zaustavlja ostale. Osporena naplata prelazi u Disputed,
    /// pa se u sljedećim ciklusima više ne pokušava.
    /// </summary>
    private async Task<int> RetryPendingRefundsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var pending = await db.Payments.AsNoTracking()
            .Where(x => x.Status == PaymentStatus.RefundPending)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var completed = 0;
        foreach (var paymentId in pending)
        {
            try
            {
                var payment = await db.Payments
                    .Include(x => x.Subscription).ThenInclude(x => x.ClientProfile).ThenInclude(x => x.User)
                    .FirstAsync(x => x.Id == paymentId, cancellationToken);
                if (await workflow.RetryPendingRefundAsync(payment, now, cancellationToken)) completed++;
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Refund retry of payment {PaymentId} failed; it will be retried in the next run.", paymentId);
                db.ChangeTracker.Clear();
            }
        }
        return completed;
    }

    private IQueryable<Core.Entities.Subscription> SubscriptionsWithUsers() => db.Subscriptions
        .Include(x => x.ClientProfile).ThenInclude(x => x.User)
        .Include(x => x.MentorProfile).ThenInclude(x => x.User);
}
