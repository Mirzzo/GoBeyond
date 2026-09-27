namespace GoBeyond.Core.Entities;

public class TrainingSession : BaseEntity
{
    public int TrainingPlanId { get; set; }
    public int DayPlanId { get; set; }
    public int ClientProfileId { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public int Repetitions { get; set; }
    public string? Note { get; set; }

    public TrainingPlan TrainingPlan { get; set; } = null!;
    public DayPlan DayPlan { get; set; } = null!;
    public ClientProfile ClientProfile { get; set; } = null!;
}
