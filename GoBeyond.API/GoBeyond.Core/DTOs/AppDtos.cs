using System.ComponentModel.DataAnnotations;

namespace GoBeyond.Core.DTOs;

public sealed record FileReferenceDto(
    string FileName,
    string FileUrl);

public sealed record OverviewMetricDto(
    string Label,
    string Value,
    string Delta);

public sealed record OverviewTrendPointDto(
    string Label,
    int Value);

public sealed record AdminOverviewReportDto(
    IReadOnlyList<OverviewMetricDto> Metrics,
    IReadOnlyList<OverviewTrendPointDto> MonthlyClients,
    IReadOnlyList<OverviewTrendPointDto> MonthlyRevenue,
    IReadOnlyList<string> Alerts);

public sealed record AdminMentorRequestDto(
    int Id,
    int UserId,
    string FullName,
    string Email,
    string Category,
    decimal Price,
    string Bio,
    int Age,
    string Status,
    int CertificateCount,
    IReadOnlyList<FileReferenceDto> Certificates);

public sealed record AdminUserListItemDto(
    int UserId,
    int? ProfileId,
    string FullName,
    string Email,
    string Role,
    bool IsActive,
    string? Category,
    string? FitnessLevel,
    int ActiveSubscriptions,
    decimal? Price,
    string Status,
    DateTime CreatedAt);

public sealed record AdminSubscriptionListItemDto(
    int Id,
    string ClientName,
    string ClientEmail,
    string MentorName,
    string MentorEmail,
    string Status,
    decimal AmountPaid,
    DateTime StartDate,
    DateTime EndDate,
    string PaymentStatus,
    string PrimaryGoal,
    bool HasPublishedPlan);

public sealed record MentorClientSnapshotDto(
    string ClientName,
    string Goal,
    string Status,
    DateTime StartDate);

public sealed record MentorReportDto(
    int MentorId,
    string MentorName,
    string Email,
    string Category,
    string Status,
    decimal Price,
    int ActiveSubscribers,
    int PendingRequests,
    int PublishedPlans,
    decimal TotalRevenue,
    double AverageRating,
    int ReviewCount,
    IReadOnlyList<MentorClientSnapshotDto> RecentClients);

public sealed record MentorSummaryDto(
    int Id,
    int UserId,
    string FullName,
    string Email,
    string Category,
    decimal Price,
    string Bio,
    string Status,
    double AverageRating,
    int ReviewCount,
    int ActiveClients,
    string City,
    string Headline,
    string ResponseTimeLabel,
    string NextStartLabel,
    string ReviewQuote,
    IReadOnlyList<string> Specialties,
    string? ProfileImageUrl,
    int AccentColorValue);

public sealed record MentorDetailDto(
    int Id,
    int UserId,
    string FullName,
    string Email,
    string Category,
    decimal Price,
    string Bio,
    int Age,
    string Status,
    double AverageRating,
    int ReviewCount,
    int ActiveClients,
    string City,
    string Headline,
    string ResponseTimeLabel,
    string NextStartLabel,
    string ReviewQuote,
    IReadOnlyList<string> Specialties,
    IReadOnlyList<FileReferenceDto> Certificates,
    string? ProfileImageUrl);

public sealed record ReviewDto(
    int Id,
    int ClientUserId,
    string ClientName,
    int Rating,
    string Comment);

public sealed record ClientQuestionnaireDto(
    string PrimaryGoal,
    string TimeCommitment,
    string HealthIssues,
    string Medications,
    string WeeklyAvailability,
    string PhysicalActivityLevel);

public sealed record CollaborationRequestDto(
    int SubscriptionId,
    int ClientUserId,
    string ClientName,
    string ClientEmail,
    int ClientAge,
    string FitnessLevel,
    decimal Weight,
    decimal Height,
    DateTime RequestedAt,
    decimal AmountPaid,
    ClientQuestionnaireDto Questionnaire);

public sealed record MentorSubscriberDto(
    int SubscriptionId,
    int ClientUserId,
    string ClientName,
    string ClientEmail,
    string Status,
    DateTime StartDate,
    DateTime EndDate,
    bool HasPublishedPlan,
    string? CurrentPlanStatus,
    string? LastCheckInLabel,
    string PrimaryGoal);

public sealed record ProgressEntryDto(
    int Id,
    int Year,
    int Month,
    decimal? Weight,
    string? Measurements,
    string? Strength,
    string? Conditioning,
    string? PhotoUrl,
    string Title,
    string Subtitle,
    string Metric,
    bool Positive);

public sealed record ProgressSummaryDto(
    string LatestWeightLabel,
    string CheckInCountLabel,
    string StrengthLabel,
    string ConditioningLabel,
    DateTime? LastUpdatedAt);

public sealed record ProgressHistoryResponseDto(
    ProgressSummaryDto Summary,
    IReadOnlyList<ProgressEntryDto> Entries);

public sealed record ClientDetailDto(
    int UserId,
    string FullName,
    string Email,
    int Age,
    decimal Weight,
    decimal Height,
    string FitnessLevel,
    string SubscriptionStatus,
    DateTime SubscriptionStartDate,
    DateTime SubscriptionEndDate,
    decimal AmountPaid,
    ClientQuestionnaireDto? Questionnaire,
    ProgressSummaryDto ProgressSummary,
    IReadOnlyList<ProgressEntryDto> RecentProgress);

public sealed record PaymentDto(
    int Id,
    string Status,
    decimal Amount,
    string Currency,
    string PaymentIntentId);

public sealed record SubscriptionDto(
    int Id,
    int MentorId,
    string MentorName,
    string MentorEmail,
    string Status,
    DateTime StartDate,
    DateTime EndDate,
    decimal AmountPaid,
    string PaymentStatus,
    string PlanName,
    string RenewalLabel,
    string CheckInDay,
    string ProgressLabel,
    string? PrimaryGoal,
    bool HasPublishedPlan);

public sealed record SubscriptionDetailDto(
    SubscriptionDto Subscription,
    ClientQuestionnaireDto? Questionnaire,
    IReadOnlyList<PaymentDto> Payments);

public sealed record DayPlanDto(
    int Id,
    string DayOfWeek,
    string TrainingDuration,
    string TrainingDescription,
    string NutritionDuration,
    string NutritionDescription);

public sealed record TrainingPlanSummaryDto(
    int Id,
    int SubscriptionId,
    int ClientUserId,
    string ClientName,
    string MentorName,
    int WeekNumber,
    string Status,
    string MotivationalQuote,
    int DayCount,
    string FocusTitle,
    string FocusSummary,
    string NextSessionTitle,
    string NextSessionDuration,
    int CompletedSessions,
    int TotalSessions);

public sealed record TrainingPlanDetailDto(
    int Id,
    int SubscriptionId,
    int ClientUserId,
    string ClientName,
    int WeekNumber,
    string Status,
    string MotivationalQuote,
    string FocusTitle,
    string FocusSummary,
    string NextSessionTitle,
    string NextSessionDuration,
    int CompletedSessions,
    int TotalSessions,
    IReadOnlyList<DayPlanDto> Days);

public sealed record PaymentIntentResultDto(
    PaymentDto Payment,
    SubscriptionDto Subscription,
    string Message);

public sealed record NotificationDto(
    int Id,
    string Title,
    string Body,
    string Type,
    bool IsRead);

public sealed record UploadedFileDto(
    string FileName,
    string Url);

public sealed class CreateSubscriptionRequestDto
{
    [Range(1, int.MaxValue)]
    public int MentorId { get; init; }

    [Required]
    [MinLength(4)]
    public string PrimaryGoal { get; init; } = string.Empty;

    [Required]
    [MinLength(2)]
    public string TimeCommitment { get; init; } = string.Empty;

    public string HealthIssues { get; init; } = string.Empty;

    public string Medications { get; init; } = string.Empty;

    [Required]
    [MinLength(2)]
    public string WeeklyAvailability { get; init; } = string.Empty;

    [Required]
    [MinLength(2)]
    public string PhysicalActivityLevel { get; init; } = string.Empty;
}

public sealed class UpdateMentorProfileRequestDto
{
    [Required]
    [MinLength(10)]
    public string Bio { get; init; } = string.Empty;

    [Range(18, 80)]
    public int Age { get; init; }

    [Required]
    public string Category { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.01", "10000")]
    public decimal Price { get; init; }
}

public sealed class UploadCertificateRequestDto
{
    [Required]
    [MinLength(3)]
    public string FileName { get; init; } = string.Empty;

    [Required]
    [MinLength(3)]
    public string FileUrl { get; init; } = string.Empty;
}

public sealed class UpsertTrainingPlanRequestDto
{
    [Range(1, int.MaxValue)]
    public int SubscriptionId { get; init; }

    [Range(1, 52)]
    public int WeekNumber { get; init; }

    [Required]
    [MinLength(6)]
    public string MotivationalQuote { get; init; } = string.Empty;

    [MinLength(1)]
    public IReadOnlyList<UpsertTrainingPlanDayRequestDto> Days { get; init; } = [];
}

public sealed class UpsertTrainingPlanDayRequestDto
{
    [Required]
    public string DayOfWeek { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int TrainingDurationMinutes { get; init; }

    [Required]
    [MinLength(4)]
    public string TrainingDescription { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int NutritionDurationMinutes { get; init; }

    [Required]
    [MinLength(4)]
    public string NutritionDescription { get; init; } = string.Empty;
}

public sealed class CreateProgressEntryRequestDto
{
    [Range(2000, 9999)]
    public int? Year { get; init; }

    [Range(1, 12)]
    public int? Month { get; init; }

    [Range(typeof(decimal), "0", "1000")]
    public decimal? Weight { get; init; }

    public string? Measurements { get; init; }

    public string? Strength { get; init; }

    public string? Conditioning { get; init; }
}

public sealed class UploadProgressPhotoRequestDto
{
    [Required]
    [MinLength(3)]
    public string PhotoUrl { get; init; } = string.Empty;
}

public sealed class CreateReviewRequestDto
{
    [Range(1, int.MaxValue)]
    public int MentorId { get; init; }

    [Range(1, 5)]
    public int Rating { get; init; }

    [Required]
    [MinLength(4)]
    public string Comment { get; init; } = string.Empty;
}

public sealed class CreatePaymentIntentRequestDto
{
    [Range(1, int.MaxValue)]
    public int SubscriptionId { get; init; }
}

public sealed class ProcessPaymentWebhookRequestDto
{
    [Required]
    [MinLength(3)]
    public string PaymentIntentId { get; init; } = string.Empty;

    [Required]
    public string Status { get; init; } = string.Empty;
}
