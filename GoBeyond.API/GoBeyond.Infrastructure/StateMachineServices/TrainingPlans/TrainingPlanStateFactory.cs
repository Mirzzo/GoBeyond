using GoBeyond.Core.Enums;

namespace GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;

public class TrainingPlanStateFactory
{
    private readonly IReadOnlyDictionary<TrainingPlanStatus, ITrainingPlanState> _states;

    public TrainingPlanStateFactory(IEnumerable<ITrainingPlanState> states)
    {
        _states = states.ToDictionary(
            x => Enum.Parse<TrainingPlanStatus>(x.Name),
            x => x);
    }

    public ITrainingPlanState Resolve(TrainingPlanStatus status)
    {
        if (_states.TryGetValue(status, out var state))
        {
            return state;
        }

        throw new InvalidOperationException($"No training plan state registered for status {status}.");
    }
}
