namespace GoBeyond.Infrastructure.Messaging;

public interface INotificationPublisher
{
    // Enqueues within the caller's EF unit of work; caller saves its business change and message together.
    Task PublishAsync(string eventType, string recipientEmail, string subject, string body,
        CancellationToken cancellationToken = default);
}
