namespace GoBeyond.Contracts.Messages;

public sealed record TrainingPlanPublishedMessage(
    int TrainingPlanId,
    int MentorUserId,
    int ClientUserId,
    DateTime PublishedAt
);
