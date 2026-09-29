using GoBeyond.Core.Enums;
using GoBeyond.Tests.TestInfrastructure;

namespace GoBeyond.Tests.Subscriptions;

/// <summary>
/// Podsjetnici SubscriptionLifecycleService-a (istek, izostanak plana) i istovremena izmjena pretplate nakon što ju je
/// ciklus odabrao: produženje se ne pregazi niti se šalje zastario datum isteka, a otkazana pretplata ne obara podsjetnike
/// za ostale pretplate.
/// </summary>
public sealed class LifecycleReminderConcurrencyTests : IDisposable
{
    private const string ExpiringQuery = "\"ExpiryReminderSentAt\" IS NULL";
    private const string MissingPlanQuery = "\"PlanMissingReminderSentAt\" IS NULL";

    private readonly SubscriptionTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ExpiringReminder_WhenARenewalIsAppliedMeanwhile_IsNotSentWithTheOldDate()
    {
        var now = DateTime.UtcNow;
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.Active, x => x.EndDate = now.AddDays(2));
        var paymentId = await _db.AddPaymentAsync(id, "pi_renewal", PaymentStatus.Pending, "succeeded", PaymentPurpose.Renewal,
            createdAt: now.AddMinutes(-1));
        // Klijent potvrđuje plaćeno produženje odmah nakon što je ciklus odabrao pretplate koje uskoro ističu.
        var renewal = new AfterQueryInterceptor(ExpiringQuery,
            () => _db.RunAsync(db => _db.Payments(db).ConfirmAsync(_db.ClientUser.Id, paymentId)));
        await using var db = _db.CreateContext(renewal);

        var result = await _db.LifecycleProcessor(db).RunAsync(now);

        Assert.True(renewal.Fired);
        Assert.Equal(0, result.ExpiringReminders);
        var subscription = await _db.SubscriptionAsync(id);
        Assert.True(subscription.EndDate > now.AddDays(31), $"EndDate {subscription.EndDate:O}");
        // Podsjetnik za novi period i dalje stiže (obnova ga je resetovala).
        Assert.Null(subscription.ExpiryReminderSentAt);
        Assert.DoesNotContain(await _db.NotificationsAsync(_db.ClientUser.Id), x => x.Type == NotificationType.SubscriptionExpiring);
    }

    [Fact]
    public async Task ExpiringReminders_WhenOneSubscriptionIsCancelledMeanwhile_StillRemindTheOthers()
    {
        var now = DateTime.UtcNow;
        var cancelled = await _db.AddSubscriptionAsync(SubscriptionStatus.Active, x => x.EndDate = now.AddDays(2));
        var other = await _db.AddSubscriptionAsync(SubscriptionStatus.Active, x => x.EndDate = now.AddDays(1),
            clientProfileId: _db.SecondClientProfileId);
        var cancel = new AfterQueryInterceptor(ExpiringQuery,
            () => _db.RunAsync(db => _db.Subscriptions(db).CancelAsync(_db.ClientUser.Id, cancelled)));
        await using var db = _db.CreateContext(cancel);

        var result = await _db.LifecycleProcessor(db).RunAsync(now);

        Assert.True(cancel.Fired);
        Assert.Equal(1, result.ExpiringReminders);
        var cancelledSubscription = await _db.SubscriptionAsync(cancelled);
        Assert.Equal(SubscriptionStatus.Cancelled, cancelledSubscription.Status);
        Assert.Null(cancelledSubscription.ExpiryReminderSentAt);
        Assert.NotNull((await _db.SubscriptionAsync(other)).ExpiryReminderSentAt);
        Assert.DoesNotContain(await _db.NotificationsAsync(_db.ClientUser.Id), x => x.Type == NotificationType.SubscriptionExpiring);
        Assert.Single(await _db.NotificationsAsync(_db.SecondClientUser.Id), x => x.Type == NotificationType.SubscriptionExpiring);
    }

    [Fact]
    public async Task MissingPlanReminders_WhenOneSubscriptionIsCancelledMeanwhile_StillRemindTheOthers()
    {
        // Obje saradnje su prihvaćene prije 20 dana, a plan nije objavljen.
        var cancelled = await _db.AddSubscriptionAsync(SubscriptionStatus.Active);
        var other = await _db.AddSubscriptionAsync(SubscriptionStatus.Active, clientProfileId: _db.SecondClientProfileId);
        var cancel = new AfterQueryInterceptor(MissingPlanQuery,
            () => _db.RunAsync(db => _db.Subscriptions(db).CancelAsync(_db.ClientUser.Id, cancelled)));
        await using var db = _db.CreateContext(cancel);

        var result = await _db.LifecycleProcessor(db).RunAsync(DateTime.UtcNow);

        Assert.True(cancel.Fired);
        Assert.Equal(1, result.PlanMissingReminders);
        Assert.Null((await _db.SubscriptionAsync(cancelled)).PlanMissingReminderSentAt);
        Assert.NotNull((await _db.SubscriptionAsync(other)).PlanMissingReminderSentAt);
        var reminder = Assert.Single(await _db.NotificationsAsync(_db.MentorUser.Id), x => x.Type == NotificationType.PlanMissing);
        Assert.Contains("Hana Kurić", reminder.Body);
    }
}
