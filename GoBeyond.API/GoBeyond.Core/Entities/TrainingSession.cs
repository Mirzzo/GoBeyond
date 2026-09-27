namespace GoBeyond.Core.Entities;

public class TrainingSession : BaseEntity
{
    public int ClientProfileId { get; set; }
    public int TrainingPlanId { get; set; }
    public int DayPlanId { get; set; }
    public int Repetitions { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public ClientProfile ClientProfile { get; set; } = null!;
    public TrainingPlan TrainingPlan { get; set; } = null!;
    public DayPlan DayPlan { get; set; } = null!;
}
