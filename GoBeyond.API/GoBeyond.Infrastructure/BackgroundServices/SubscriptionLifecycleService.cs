using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Services.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoBeyond.Infrastructure.BackgroundServices;

/// <summary>Hosted servis koji periodično (Lifecycle:IntervalSeconds) pokreće SubscriptionLifecycleProcessor.</summary>
public sealed class SubscriptionLifecycleService(
    IServiceScopeFactory scopeFactory,
    IOptions<LifecycleOptions> options,
    ILogger<SubscriptionLifecycleService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(10, options.Value.IntervalSeconds));
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, options.Value.StartupDelaySeconds)), stoppingToken);
            using var timer = new PeriodicTimer(interval);
            do
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<ISubscriptionLifecycleProcessor>();
                    await processor.RunAsync(DateTime.UtcNow, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // Svaka greška osim gašenja (i OperationCanceledException, npr. HttpClient timeout prema Stripe-u) se
                    // loguje i ponavlja u sljedećem ciklusu. Izuzetak koji napusti ExecuteAsync bi zaustavio cijeli API.
                    logger.LogError(ex, "Subscription lifecycle run failed; it will be retried in {Interval}.", interval);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // gašenje aplikacije
        }
    }
}
