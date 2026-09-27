using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoBeyond.Infrastructure.Services.Subscriptions;

public sealed record LifecycleRunResult(int Expired, int ExpiringReminders, int PlanMissingReminders, int InactivityReminders);

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
    IOptions<LifecycleOptions> options,
    ILogger<SubscriptionLifecycleProcessor> logger) : ISubscriptionLifecycleProcessor
{
    public async Task<LifecycleRunResult> RunAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var result = new LifecycleRunResult(
            await ExpireAsync(now, cancellationToken),
            await RemindExpiringAsync(now, settings, cancellationToken),
            await RemindMissingPlansAsync(now, settings, cancellationToken),
            await RemindInactiveClientsAsync(now, settings, cancellationToken));

        if (result != new LifecycleRunResult(0, 0, 0, 0))
            logger.LogInformation("Subscription lifecycle: {Result}", result);
        return result;
    }

    /// <summary>Istek saradnje: Active s prošlim EndDate → Expired, obavijest klijentu i mentoru.</summary>
    private async Task<int> ExpireAsync(DateTime now, CancellationToken cancellationToken)
    {
        var expired = await SubscriptionsWithUsers()
            .Where(x => x.Status == SubscriptionStatus.Active && x.EndDate != null && x.EndDate < now)
            .ToListAsync(cancellationToken);
        foreach (var subscription in expired) workflow.Expire(subscription, now);
        await db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }

    /// <summary>Podsjetnik klijentu N dana prije isteka (jednom po periodu; obnova resetuje podsjetnik).</summary>
    private async Task<int> RemindExpiringAsync(DateTime now, LifecycleOptions settings, CancellationToken cancellationToken)
    {
        var limit = now.AddDays(settings.ExpiringReminderDays);
        var expiring = await SubscriptionsWithUsers()
            .Where(x => x.Status == SubscriptionStatus.Active && x.ExpiryReminderSentAt == null &&
                        x.EndDate != null && x.EndDate > now && x.EndDate <= limit)
            .ToListAsync(cancellationToken);

        foreach (var subscription in expiring)
        {
            subscription.ExpiryReminderSentAt = now;
            notifications.Notify(subscription.ClientProfile.User, NotificationType.SubscriptionExpiring, "Pretplata uskoro ističe",
                $"Saradnja sa mentorom {subscription.MentorProfile.User.FullName} ističe {DomainTexts.Date(subscription.EndDate)}. " +
                "Produžite pretplatu u sekciji \"Pretplata\" kako biste zadržali svoj plan.",
                sendEmail: true);
        }
        await db.SaveChangesAsync(cancellationToken);
        return expiring.Count;
    }

    /// <summary>Izostanak plana: aktivna saradnja duže od N sati bez objavljenog plana (najviše jednom u M sati).</summary>
    private async Task<int> RemindMissingPlansAsync(DateTime now, LifecycleOptions settings, CancellationToken cancellationToken)
    {
        var acceptedBefore = now.AddHours(-settings.PlanMissingAfterHours);
        var repeatBefore = now.AddHours(-settings.PlanMissingRepeatHours);
        var missing = await SubscriptionsWithUsers()
            .Where(x => x.Status == SubscriptionStatus.Active && x.AcceptedAt != null && x.AcceptedAt <= acceptedBefore &&
                        (x.TrainingPlan == null || x.TrainingPlan.Status == TrainingPlanStatus.Draft) &&
                        (x.PlanMissingReminderSentAt == null || x.PlanMissingReminderSentAt <= repeatBefore))
            .ToListAsync(cancellationToken);

        foreach (var subscription in missing)
        {
            subscription.PlanMissingReminderSentAt = now;
            notifications.Notify(subscription.MentorProfile.User, NotificationType.PlanMissing, "Klijent čeka trening plan",
                $"Saradnja sa klijentom {subscription.ClientProfile.User.FullName} je aktivna od {DomainTexts.Date(subscription.AcceptedAt)}, " +
                "a plan još nije objavljen. Izradite i objavite plan u sekciji \"Zahtjevi za saradnju\".",
                sendEmail: true);
        }
        await db.SaveChangesAsync(cancellationToken);
        return missing.Count;
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

    private IQueryable<Core.Entities.Subscription> SubscriptionsWithUsers() => db.Subscriptions
        .Include(x => x.ClientProfile).ThenInclude(x => x.User)
        .Include(x => x.MentorProfile).ThenInclude(x => x.User);
}
