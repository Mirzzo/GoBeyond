using GoBeyond.Core.Enums;

namespace GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;

public interface ITrainingPlanStateFactory
{
    BaseTrainingPlanState GetState(TrainingPlanStatus status);
}

/// <summary>Vraća objekat stanja za trenutni status plana (stanja se registruju u DI kontejneru).</summary>
public sealed class TrainingPlanStateFactory(IEnumerable<BaseTrainingPlanState> states) : ITrainingPlanStateFactory
{
    private readonly IReadOnlyDictionary<TrainingPlanStatus, BaseTrainingPlanState> _states =
        states.ToDictionary(x => x.Status);

    public BaseTrainingPlanState GetState(TrainingPlanStatus status) =>
        _states.TryGetValue(status, out var state)
            ? state
            : throw new InvalidOperationException($"Stanje plana {status} nije registrovano.");
}
