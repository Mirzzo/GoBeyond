using GoBeyond.Core.DTOs.Plans;
using GoBeyond.Core.DTOs.Progress;
using GoBeyond.Core.DTOs.Subscriptions;
using GoBeyond.Core.Enums;

namespace GoBeyond.Core.DTOs.Mentors;

public class MentorSummaryDto
{
    public int MentorProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string? ProfileImageUrl { get; set; }
    public int TrainingTypeId { get; set; }
    public string TrainingTypeName { get; set; } = string.Empty;
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public decimal MonthlyPrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int YearsOfExperience { get; set; }
    public int Age { get; set; }
}

public sealed class MentorDetailDto : MentorSummaryDto
{
    public string Bio { get; set; } = string.Empty;
    public List<string> SpecializationNames { get; set; } = [];
    public List<ReviewDto> Reviews { get; set; } = [];
}

public sealed class ReviewDto
{
    public int Id { get; set; }
    public string ClientFullName { get; set; } = string.Empty;
    public string? ClientPhotoUrl { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsMine { get; set; }
}

public sealed class MentorRecommendationDto
{
    public MentorSummaryDto Mentor { get; set; } = new();
    public double Score { get; set; }
    public List<string> Reasons { get; set; } = [];
}

public sealed class CollaborationRequestDto
{
    public int SubscriptionId { get; set; }
    public string ClientFullName { get; set; } = string.Empty;
    public string? ClientPhotoUrl { get; set; }
    public SubscriptionStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public int? PlanId { get; set; }
    public TrainingPlanStatus? PlanStatus { get; set; }
}

public class ClientDescriptionDto
{
    public int SubscriptionId { get; set; }
    public SubscriptionStatus Status { get; set; }
    public string ClientFullName { get; set; } = string.Empty;
    public string? ClientPhotoUrl { get; set; }
    public int Age { get; set; }
    public string GenderName { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public decimal HeightCm { get; set; }
    public string FitnessLevelName { get; set; } = string.Empty;
    public int TrainingExperienceYears { get; set; }
    public string FitnessGoalName { get; set; } = string.Empty;
    public string? GoalDescription { get; set; }
    public DateTime RequestedAt { get; set; }
    public QuestionnaireDto? Questionnaire { get; set; }
}

public sealed class SubscriberDto
{
    public int SubscriptionId { get; set; }
    public string ClientFullName { get; set; } = string.Empty;
    public string? ClientPhotoUrl { get; set; }
    public SubscriptionStatus Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? PlanId { get; set; }
    public TrainingPlanStatus? PlanStatus { get; set; }
    public DateTime? LastTrainingAt { get; set; }
}

public sealed class SubscriberDetailDto : ClientDescriptionDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<TrainingSessionItemDto> Sessions { get; set; } = [];
    public List<ProgressEntryItemDto> Progress { get; set; } = [];
}
