using GoBeyond.Core.DTOs.Plans;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;

namespace GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;

/// <summary>Plan u izradi: mentor slobodno dodaje/briše dane; objava traži svih 7 dana.</summary>
public sealed class DraftTrainingPlanState : BaseTrainingPlanState
{
    public override TrainingPlanStatus Status => TrainingPlanStatus.Draft;
    public override IReadOnlyList<string> AllowedActions => ["UpdateDetails", "UpsertDay", "RemoveDay", "Publish"];

    public override void UpdateDetails(TrainingPlan plan, string? motivationalQuote, DateTime now)
    {
        plan.MotivationalQuote = motivationalQuote;
        Touch(plan, now);
    }

    public override void UpsertDay(TrainingPlan plan, int dayOfWeek, UpsertDayPlanRequest request, DateTime now)
    {
        ApplyDay(plan, dayOfWeek, request);
        Touch(plan, now);
    }

    public override void RemoveDay(TrainingPlan plan, int dayOfWeek, DateTime now)
    {
        var day = plan.Days.FirstOrDefault(x => x.DayOfWeek == dayOfWeek);
        if (day is not null) plan.Days.Remove(day);
        Touch(plan, now);
    }

    public override void Publish(TrainingPlan plan, DateTime now)
    {
        EnsureAllDaysFilled(plan);
        plan.Status = TrainingPlanStatus.Published;
        plan.PublishedAt = now;
        Touch(plan, now);
    }
}
