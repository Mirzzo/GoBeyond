using GoBeyond.Core.Entities;

namespace GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;

public interface ITrainingPlanState
{
    string Name { get; }
    Task PublishAsync(TrainingPlan trainingPlan, CancellationToken cancellationToken = default);
}
