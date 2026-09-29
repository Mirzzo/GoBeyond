using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.BackgroundServices;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Services.Subscriptions;
using GoBeyond.Tests.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Payments;

/// <summary>
/// SubscriptionLifecycleService (hosted servis): OperationCanceledException koji nije gašenje (npr. HttpClient timeout prema
/// Stripe-u) se loguje i ciklus se ponavlja - ne smije napustiti ExecuteAsync, jer bi podrazumijevani StopHost ugasio API.
/// </summary>
public sealed class LifecycleServiceTests
{
    [Fact]
    public async Task RunFailingWithHttpClientTimeout_DoesNotStopTheHostedService()
    {
        var processor = new ThrowingProcessor();
        var services = new ServiceCollection().AddScoped<ISubscriptionLifecycleProcessor>(_ => processor).BuildServiceProvider();
        var service = new SubscriptionLifecycleService(services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new LifecycleOptions { IntervalSeconds = 10, StartupDelaySeconds = 0 }),
            NullLogger<SubscriptionLifecycleService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await processor.FirstRun.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(300);

        Assert.False(service.ExecuteTask!.IsCompleted, "ExecuteAsync je završio (izuzetak bi zaustavio cijeli API).");
        await service.StopAsync(CancellationToken.None);
        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ExpiringReminder_UsesLocalDateWithASinglePeriod()
    {
        using var db = new SubscriptionTestDatabase();
        var now = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
        await db.AddSubscriptionAsync(SubscriptionStatus.Active, x =>
        {
            x.AcceptedAt = x.StartDate = now.AddDays(-28);
            x.EndDate = new DateTime(2026, 10, 1, 22, 30, 0, DateTimeKind.Utc); // 02.10.2026. u 00:30 u BiH
        });

        await db.RunAsync(context => db.LifecycleProcessor(context).RunAsync(now));

        var reminder = Assert.Single(await db.NotificationsAsync(db.ClientUser.Id), x => x.Type == NotificationType.SubscriptionExpiring);
        Assert.Equal("Saradnja sa mentorom Selma Delić ističe 02.10.2026. Produžite pretplatu u sekciji \"Pretplata\" kako biste " +
                     "zadržali svoj plan.", reminder.Body);
    }

    [Fact]
    public async Task RenewalAppliedByTheLifecycle_KeepsTheStripeChargeTimeAsPaidAt()
    {
        using var db = new SubscriptionTestDatabase();
        var now = DateTime.UtcNow;
        var chargedAt = now.AddMinutes(-1).AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
        var id = await db.AddSubscriptionAsync(SubscriptionStatus.Active, x => x.EndDate = now.AddSeconds(-20));
        await db.AddPaymentAsync(id, "pi_renewal", PaymentStatus.Pending, "succeeded", PaymentPurpose.Renewal, createdAt: now.AddMinutes(-2));
        db.Gateway.Intents["pi_renewal"] = db.Gateway.Intents["pi_renewal"] with { ChargedAt = chargedAt };

        await db.RunAsync(context => db.LifecycleProcessor(context).RunAsync(now));

        var subscription = await db.SubscriptionAsync(id);
        Assert.Equal(chargedAt, Assert.Single(subscription.Payments).PaidAt);
        // Plaćeno prije isteka: novi period se nastavlja na stari kraj.
        Assert.Equal(now.AddSeconds(-20).AddDays(30), subscription.EndDate!.Value, TimeSpan.FromMilliseconds(1));
    }

    private sealed class ThrowingProcessor : ISubscriptionLifecycleProcessor
    {
        public TaskCompletionSource FirstRun { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<LifecycleRunResult> RunAsync(DateTime now, CancellationToken cancellationToken = default)
        {
            FirstRun.TrySetResult();
            throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.");
        }
    }
}
