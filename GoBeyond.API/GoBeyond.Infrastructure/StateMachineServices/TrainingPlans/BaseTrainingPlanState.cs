using GoBeyond.Core.DTOs.Plans;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Common;

namespace GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;

/// <summary>
/// State Machine obrazac za trening plan. Svako stanje (Draft, Published, Archived) dozvoljava
/// samo svoje akcije; nedozvoljena akcija baca ValidationException (400) s porukom.
/// Stanja rade samo nad entitetom (bez baze), pa su lako testabilna.
///
///   Draft ──Publish (7 dana)──▶ Published ──Archive──▶ Archived
///                                  ▲                        │
///                                  └──────Publish (7 dana)──┘
/// </summary>
public abstract class BaseTrainingPlanState
{
    public const int DaysInWeek = 7;
    public const string SevenDaysRequired = "Plan mora imati popunjenih svih 7 dana prije objave.";

    public abstract TrainingPlanStatus Status { get; }

    /// <summary>Akcije dozvoljene u ovom stanju (za UI i dokumentaciju).</summary>
    public abstract IReadOnlyList<string> AllowedActions { get; }

    public virtual void UpdateDetails(TrainingPlan plan, string? motivationalQuote, DateTime now) =>
        throw NotAllowed("izmjena plana");

    public virtual void UpsertDay(TrainingPlan plan, int dayOfWeek, UpsertDayPlanRequest request, DateTime now) =>
        throw NotAllowed("uređivanje dana");

    public virtual void RemoveDay(TrainingPlan plan, int dayOfWeek, DateTime now) =>
        throw NotAllowed("brisanje dana");

    public virtual void Publish(TrainingPlan plan, DateTime now) =>
        throw NotAllowed("objava");

    public virtual void Archive(TrainingPlan plan, DateTime now) =>
        throw NotAllowed("arhiviranje");

    protected ValidationException NotAllowed(string action) =>
        new($"Akcija \"{action}\" nije dozvoljena za plan u statusu \"{DomainTexts.PlanStatusName(Status)}\".");

    protected static void ApplyDay(TrainingPlan plan, int dayOfWeek, UpsertDayPlanRequest request)
    {
        var day = plan.Days.FirstOrDefault(x => x.DayOfWeek == dayOfWeek);
        if (day is null)
        {
            day = new DayPlan { DayOfWeek = dayOfWeek };
            plan.Days.Add(day);
        }
        day.TrainingDurationMinutes = request.TrainingDurationMinutes;
        day.TrainingDescription = request.TrainingDescription.Trim();
        day.NutritionDurationMinutes = request.NutritionDurationMinutes;
        day.NutritionDescription = request.NutritionDescription.Trim();
    }

    protected static void EnsureAllDaysFilled(TrainingPlan plan)
    {
        var filled = plan.Days.Select(x => x.DayOfWeek).Where(x => x is >= 1 and <= DaysInWeek).Distinct().Count();
        if (filled < DaysInWeek) throw new ValidationException(SevenDaysRequired);
    }

    protected static void Touch(TrainingPlan plan, DateTime now) => plan.UpdatedAt = now;
}
