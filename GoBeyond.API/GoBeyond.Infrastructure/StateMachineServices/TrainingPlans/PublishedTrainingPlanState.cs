using GoBeyond.Core.Entities;

namespace GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;

public class PublishedTrainingPlanState : ITrainingPlanState
{
    public string Name => "Published";

    public Task PublishAsync(TrainingPlan trainingPlan, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Plan is already published.");
    }
}
