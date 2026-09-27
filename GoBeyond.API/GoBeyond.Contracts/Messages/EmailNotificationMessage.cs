namespace GoBeyond.Contracts.Messages;

public sealed record EmailNotificationMessage(
    int MessageId, string EventType, string RecipientEmail, string Subject, string Body);
