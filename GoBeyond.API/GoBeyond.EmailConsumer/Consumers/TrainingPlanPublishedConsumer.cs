using GoBeyond.Contracts.Messages;
using GoBeyond.EmailConsumer.Services;

namespace GoBeyond.EmailConsumer.Consumers;

public class TrainingPlanPublishedConsumer(
    IEmailSender emailSender,
    ILogger<TrainingPlanPublishedConsumer> logger)
{
    public async Task HandleAsync(TrainingPlanPublishedMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Handling training plan published message: {TrainingPlanId}",
            message.TrainingPlanId);

        await emailSender.SendAsync(
            "client@gobeyond.local",
            "Your plan is ready",
            $"Training plan {message.TrainingPlanId} was published at {message.PublishedAt:O}.",
            cancellationToken);
    }
}
