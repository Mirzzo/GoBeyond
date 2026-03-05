using GoBeyond.Contracts.Messages;
using GoBeyond.EmailConsumer.Services;

namespace GoBeyond.EmailConsumer.Consumers;

public class SubscriptionActivatedConsumer(
    IEmailSender emailSender,
    ILogger<SubscriptionActivatedConsumer> logger)
{
    public async Task HandleAsync(SubscriptionActivatedMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Handling subscription activated message: {SubscriptionId}",
            message.SubscriptionId);

        await emailSender.SendAsync(
            "mentor@gobeyond.local",
            "New subscriber activated",
            $"Subscription {message.SubscriptionId} was activated at {message.ActivatedAt:O}.",
            cancellationToken);
    }
}
