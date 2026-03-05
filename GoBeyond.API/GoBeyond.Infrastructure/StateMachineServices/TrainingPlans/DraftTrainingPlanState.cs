using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;

namespace GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;

public class DraftTrainingPlanState : ITrainingPlanState
{
    public string Name => nameof(TrainingPlanStatus.Draft);

    public Task PublishAsync(TrainingPlan trainingPlan, CancellationToken cancellationToken = default)
    {
        trainingPlan.Status = TrainingPlanStatus.Published;
        return Task.CompletedTask;
    }
}
