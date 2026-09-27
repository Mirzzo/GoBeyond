using GoBeyond.Core.DTOs.Plans;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;

namespace GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;

/// <summary>Arhiviran plan: klijent ga i dalje vidi; mentor ga može doraditi i ponovo objaviti.</summary>
public sealed class ArchivedTrainingPlanState : BaseTrainingPlanState
{
    public override TrainingPlanStatus Status => TrainingPlanStatus.Archived;
    public override IReadOnlyList<string> AllowedActions => ["UpdateDetails", "UpsertDay", "Publish"];

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

    public override void Publish(TrainingPlan plan, DateTime now)
    {
        EnsureAllDaysFilled(plan);
        plan.Status = TrainingPlanStatus.Published;
        plan.Version++;
        plan.PublishedAt = now;
        Touch(plan, now);
    }
}
