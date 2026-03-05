namespace GoBeyond.Contracts.Messages;

public sealed record SubscriptionActivatedMessage(
    int SubscriptionId,
    int MentorUserId,
    int ClientUserId,
    DateTime ActivatedAt
);
