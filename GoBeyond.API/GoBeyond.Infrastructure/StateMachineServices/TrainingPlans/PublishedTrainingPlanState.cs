using GoBeyond.Core.DTOs.Plans;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;

namespace GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;

/// <summary>Objavljen plan: svaka izmjena povećava verziju (klijent dobija PlanUpdated obavijest).</summary>
public sealed class PublishedTrainingPlanState : BaseTrainingPlanState
{
    public override TrainingPlanStatus Status => TrainingPlanStatus.Published;
    public override IReadOnlyList<string> AllowedActions => ["UpdateDetails", "UpsertDay", "Archive"];

    public override void UpdateDetails(TrainingPlan plan, string? motivationalQuote, DateTime now)
    {
        plan.MotivationalQuote = motivationalQuote;
        plan.Version++;
        Touch(plan, now);
    }

    public override void UpsertDay(TrainingPlan plan, int dayOfWeek, UpsertDayPlanRequest request, DateTime now)
    {
        ApplyDay(plan, dayOfWeek, request);
        plan.Version++;
        Touch(plan, now);
    }

    public override void Archive(TrainingPlan plan, DateTime now)
    {
        plan.Status = TrainingPlanStatus.Archived;
        Touch(plan, now);
    }
}
