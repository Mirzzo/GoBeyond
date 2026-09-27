using System.Text.Json;
using GoBeyond.Core.DTOs.Plans;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;

namespace GoBeyond.Infrastructure.Common;

/// <summary>
/// Mapiranje trening plana u DTO i JSON snapshot ("HISTORIJA PLANA").
/// Očekuje učitane: Subscription, MentorProfile.User, ClientProfile.User i Days.
/// </summary>
public static class PlanMapper
{
    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);

    /// <summary>Plan se može uređivati samo dok je pretplata aktivna i mentor nije obrisan.</summary>
    public static bool CanEdit(TrainingPlan plan) =>
        plan.Subscription.Status == SubscriptionStatus.Active && !plan.MentorProfile.User.IsDeleted;

    public static PlanDetailDto ToDetail(TrainingPlan plan) => new()
    {
        Id = plan.Id,
        SubscriptionId = plan.SubscriptionId,
        MentorFullName = plan.MentorProfile.User.FullName,
        ClientFullName = plan.ClientProfile.User.FullName,
        MotivationalQuote = plan.MotivationalQuote,
        Status = plan.Status,
        Version = plan.Version,
        CanEdit = CanEdit(plan),
        CreatedAt = plan.CreatedAt,
        UpdatedAt = plan.UpdatedAt,
        PublishedAt = plan.PublishedAt,
        Days = plan.Days.OrderBy(x => x.DayOfWeek).Select(ToDay).ToList()
    };

    public static PlanSummaryDto ToSummary(TrainingPlan plan) => new()
    {
        Id = plan.Id,
        SubscriptionId = plan.SubscriptionId,
        ClientFullName = plan.ClientProfile.User.FullName,
        ClientPhotoUrl = plan.ClientProfile.User.ProfileImageUrl,
        Status = plan.Status,
        Version = plan.Version,
        FilledDays = plan.Days.Count,
        CreatedAt = plan.CreatedAt,
        UpdatedAt = plan.UpdatedAt,
        PublishedAt = plan.PublishedAt,
        SubscriptionStatus = plan.Subscription.Status,
        CanEdit = CanEdit(plan)
    };

    public static DayPlanDto ToDay(DayPlan day) => new()
    {
        Id = day.Id,
        DayOfWeek = day.DayOfWeek,
        DayName = BosnianCalendar.DayName(day.DayOfWeek),
        TrainingDurationMinutes = day.TrainingDurationMinutes,
        TrainingDescription = day.TrainingDescription,
        NutritionDurationMinutes = day.NutritionDurationMinutes,
        NutritionDescription = day.NutritionDescription
    };

    /// <summary>Snapshot je read-only kopija plana u trenutku unosa napretka.</summary>
    public static string ToSnapshotJson(TrainingPlan plan)
    {
        var detail = ToDetail(plan);
        detail.CanEdit = false;
        return JsonSerializer.Serialize(detail, SnapshotJson);
    }

    public static PlanDetailDto? FromSnapshotJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var detail = JsonSerializer.Deserialize<PlanDetailDto>(json, SnapshotJson);
        if (detail is not null) detail.CanEdit = false;
        return detail;
    }
}
