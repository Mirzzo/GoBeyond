using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Services.Payments;
using GoBeyond.Tests.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GoBeyond.Tests.Payments;

/// <summary>
/// SubscriptionLifecycleService: greška Stripe-a (npr. HttpClient timeout) kod jedne uplate ne zaustavlja ostale uplate
/// ni ostale korake (istek, povrati), a produženje plaćeno tik prije isteka produžava pretplatu umjesto da bude vraćeno.
/// </summary>
public sealed class LifecycleRobustnessTests : IDisposable
{
    private readonly SubscriptionTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Reconcile_WhenStripeTimesOutForOnePayment_StillProcessesTheOthers()
    {
        var hanging = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        await _db.AddPaymentAsync(hanging, "pi_hang", PaymentStatus.Pending, "succeeded", createdAt: DateTime.UtcNow.AddMinutes(-20));
        var paid = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment, clientProfileId: _db.SecondClientProfileId);
        await _db.AddPaymentAsync(paid, "pi_paid", PaymentStatus.Pending, "succeeded", createdAt: DateTime.UtcNow.AddMinutes(-10));
        _db.Gateway.BeforeGetIntent = id => id == "pi_hang"
            ? throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.")
            : Task.CompletedTask;

        var applied = await _db.RunAsync(db => _db.Payments(db).ReconcilePendingAsync(DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(-48)));

        Assert.Equal(1, applied);
        Assert.Equal(SubscriptionStatus.AwaitingMentor, (await _db.SubscriptionAsync(paid)).Status);
        var stuck = await _db.SubscriptionAsync(hanging);
        Assert.Equal(SubscriptionStatus.PendingPayment, stuck.Status);
        Assert.Equal(PaymentStatus.Pending, Assert.Single(stuck.Payments).Status);
    }

    [Fact]
    public async Task Reconcile_WhenStripeIsUnreachable_PostponesTheRemainingPaymentsToTheNextRun()
    {
        // Zaglavljen Stripe: svaka provjera bi čekala puni timeout (30 s) i odgodila istek u istom ciklusu.
        var first = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        var second = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment, clientProfileId: _db.SecondClientProfileId);
        var third = await _db.AddSubscriptionAsync(SubscriptionStatus.Cancelled, clientProfileId: _db.SecondClientProfileId);
        foreach (var (id, minutesAgo) in new[] { (first, 20), (second, 15), (third, 10) })
            await _db.AddPaymentAsync(id, $"pi_{id}", PaymentStatus.Pending, "succeeded", createdAt: DateTime.UtcNow.AddMinutes(-minutesAgo));
        var stripeCalls = 0;
        _db.Gateway.BeforeGetIntent = _ =>
        {
            Interlocked.Increment(ref stripeCalls);
            throw new ValidationException(StripePaymentGateway.CommunicationFailed);
        };

        var applied = await _db.RunAsync(db => _db.Payments(db).ReconcilePendingAsync(DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(-48)));

        Assert.Equal(0, applied);
        Assert.Equal(1, stripeCalls);
        Assert.All(await _db.RunAsync(db => db.Payments.AsNoTracking().ToListAsync()), x => Assert.Equal(PaymentStatus.Pending, x.Status));
    }

    [Fact]
    public async Task Expiry_WhenStripeIsUnreachable_ExpiresTheOthersWithoutWaitingForStripeAgain()
    {
        var now = DateTime.UtcNow;
        var ids = new List<int>();
        foreach (var client in new[] { _db.ClientProfileId, _db.SecondClientProfileId })
        {
            var id = await _db.AddSubscriptionAsync(SubscriptionStatus.Active, x => x.EndDate = now.AddMinutes(-1), clientProfileId: client);
            // Mlađe od praga usklađivanja: provjerava ih samo istek.
            await _db.AddPaymentAsync(id, $"pi_renewal_{id}", PaymentStatus.Pending, "requires_payment_method", PaymentPurpose.Renewal,
                createdAt: now.AddMinutes(-1));
            ids.Add(id);
        }
        var stripeCalls = 0;
        _db.Gateway.BeforeGetIntent = _ =>
        {
            Interlocked.Increment(ref stripeCalls);
            throw new ValidationException(StripePaymentGateway.CommunicationFailed);
        };

        var result = await _db.RunAsync(db => _db.LifecycleProcessor(db).RunAsync(now));

        Assert.Equal(2, result.Expired);
        Assert.Equal(1, stripeCalls);
        foreach (var id in ids)
        {
            var subscription = await _db.SubscriptionAsync(id);
            Assert.Equal(SubscriptionStatus.Expired, subscription.Status);
            Assert.Equal(PaymentStatus.Pending, Assert.Single(subscription.Payments).Status);
        }
    }

    [Fact]
    public async Task LifecycleRun_WhenStripeTimesOut_StillExpiresSubscriptions()
    {
        var expired = await _db.AddSubscriptionAsync(SubscriptionStatus.Active, x => x.EndDate = DateTime.UtcNow.AddMinutes(-1));
        var pending = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment, clientProfileId: _db.SecondClientProfileId);
        await _db.AddPaymentAsync(pending, "pi_hang", PaymentStatus.Pending, "requires_payment_method", createdAt: DateTime.UtcNow.AddMinutes(-10));
        _db.Gateway.BeforeGetIntent = _ => throw new TaskCanceledException("HttpClient.Timeout");

        var result = await _db.RunAsync(db => _db.LifecycleProcessor(db).RunAsync(DateTime.UtcNow));

        Assert.Equal(1, result.Expired);
        Assert.Equal(SubscriptionStatus.Expired, (await _db.SubscriptionAsync(expired)).Status);
    }

    [Fact]
    public async Task RenewalPaidShortlyBeforeEndDate_ExtendsTheSubscriptionInsteadOfExpiringIt()
    {
        // Plaćeno 1 min prije isteka, confirm izgubljen: uplata je mlađa od praga usklađivanja (5 min), ali se prije
        // isteka ipak provjerava na Stripe-u.
        var now = DateTime.UtcNow;
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.Active, x => x.EndDate = now.AddSeconds(-20));
        await _db.AddPaymentAsync(id, "pi_renewal", PaymentStatus.Pending, "succeeded", PaymentPurpose.Renewal, createdAt: now.AddMinutes(-1));

        var result = await _db.RunAsync(db => _db.LifecycleProcessor(db).RunAsync(now));

        Assert.Equal(0, result.Expired);
        var subscription = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.True(subscription.EndDate > now.AddDays(29), $"EndDate {subscription.EndDate:O}");
        Assert.Equal(PaymentStatus.Succeeded, Assert.Single(subscription.Payments).Status);
        Assert.Empty(_db.Gateway.Refunds);
        Assert.DoesNotContain(await _db.NotificationsAsync(_db.ClientUser.Id), x => x.Type == NotificationType.SubscriptionExpired);
    }

    [Fact]
    public async Task ExpiringSubscription_ClosesItsUnpaidRenewalIntent()
    {
        var now = DateTime.UtcNow;
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.Active, x => x.EndDate = now.AddMinutes(-1));
        await _db.AddPaymentAsync(id, "pi_renewal", PaymentStatus.Pending, "requires_payment_method", PaymentPurpose.Renewal,
            createdAt: now.AddHours(-60));

        var result = await _db.RunAsync(db => _db.LifecycleProcessor(db).RunAsync(now));

        Assert.Equal(1, result.Expired);
        Assert.Equal([("pi_renewal", "cancel:pi_renewal")], _db.Gateway.Cancels);
        Assert.Equal(PaymentStatus.Failed, Assert.Single((await _db.SubscriptionAsync(id)).Payments).Status);
    }

    [Fact]
    public async Task ReminderConflictingWithAConcurrentCancel_DoesNotSkipTheRemainingSteps()
    {
        // Klijent otkazuje pretplatu baš dok ciklus snima podsjetnik o isteku: status je concurrency token, pa snimanje
        // podsjetnika ne uspijeva. Ostali koraci (povrati) se ipak izvršavaju, a otkazivanje ostaje.
        var expiring = await _db.AddSubscriptionAsync(SubscriptionStatus.Active, x => x.EndDate = DateTime.UtcNow.AddDays(2));
        var cancelled = await _db.AddSubscriptionAsync(SubscriptionStatus.Cancelled, clientProfileId: _db.SecondClientProfileId);
        await _db.AddPaymentAsync(cancelled, "pi_refund", PaymentStatus.RefundPending, "succeeded");
        var concurrentCancel = new BeforeSaveInterceptor(context =>
            context.ChangeTracker.Entries<Subscription>().Any(x => x.Entity.Id == expiring && x.Property(s => s.ExpiryReminderSentAt).IsModified),
            () => _db.RunAsync(db => db.Subscriptions.Where(x => x.Id == expiring)
                .ExecuteUpdateAsync(x => x.SetProperty(s => s.Status, SubscriptionStatus.Cancelled))));
        await using var db = _db.CreateContext(concurrentCancel);

        var result = await _db.LifecycleProcessor(db).RunAsync(DateTime.UtcNow);

        Assert.True(concurrentCancel.Fired);
        Assert.Equal(0, result.ExpiringReminders);
        Assert.Equal(1, result.RefundsCompleted);
        Assert.Equal(PaymentStatus.Refunded, Assert.Single((await _db.SubscriptionAsync(cancelled)).Payments).Status);
        var subscription = await _db.SubscriptionAsync(expiring);
        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
        Assert.Null(subscription.ExpiryReminderSentAt);
        Assert.DoesNotContain(await _db.NotificationsAsync(_db.ClientUser.Id), x => x.Type == NotificationType.SubscriptionExpiring);
    }

    [Fact]
    public async Task RefundRetry_WhenOneRefundFailsUnexpectedly_StillRetriesTheOthers()
    {
        var first = await _db.AddSubscriptionAsync(SubscriptionStatus.Cancelled);
        await _db.AddPaymentAsync(first, "pi_a", PaymentStatus.RefundPending, "succeeded");
        var second = await _db.AddSubscriptionAsync(SubscriptionStatus.Cancelled, clientProfileId: _db.SecondClientProfileId);
        await _db.AddPaymentAsync(second, "pi_b", PaymentStatus.RefundPending, "succeeded");
        _db.Gateway.BeforeRefund = id => id == "pi_a" ? throw new ConflictException(PaymentService.StillProcessing) : Task.CompletedTask;

        var result = await _db.RunAsync(db => _db.LifecycleProcessor(db).RunAsync(DateTime.UtcNow));

        Assert.Equal(1, result.RefundsCompleted);
        Assert.Equal(PaymentStatus.Refunded, Assert.Single((await _db.SubscriptionAsync(second)).Payments).Status);
        Assert.Equal(PaymentStatus.RefundPending, Assert.Single((await _db.SubscriptionAsync(first)).Payments).Status);
    }

    /// <summary>Jednom, neposredno prije snimanja koje zadovoljava uslov, izvrši "istovremenu" izmjenu na drugoj konekciji.</summary>
    private sealed class BeforeSaveInterceptor(Func<DbContext, bool> when, Func<Task> concurrentChange) : SaveChangesInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Fired && eventData.Context is { } context && when(context))
            {
                Fired = true;
                await concurrentChange();
            }
            return result;
        }
    }
}
