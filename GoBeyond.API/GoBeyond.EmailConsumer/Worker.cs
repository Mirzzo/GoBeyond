using GoBeyond.Contracts.Messages;
using GoBeyond.EmailConsumer.Consumers;

namespace GoBeyond.EmailConsumer;

public class Worker(
    ILogger<Worker> logger,
    SubscriptionActivatedConsumer subscriptionActivatedConsumer,
    TrainingPlanPublishedConsumer trainingPlanPublishedConsumer) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Email consumer worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await subscriptionActivatedConsumer.HandleAsync(
                new SubscriptionActivatedMessage(
                    SubscriptionId: 1,
                    MentorUserId: 2,
                    ClientUserId: 3,
                    ActivatedAt: DateTime.UtcNow),
                stoppingToken);

            await trainingPlanPublishedConsumer.HandleAsync(
                new TrainingPlanPublishedMessage(
                    TrainingPlanId: 1,
                    MentorUserId: 2,
                    ClientUserId: 3,
                    PublishedAt: DateTime.UtcNow),
                stoppingToken);

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
