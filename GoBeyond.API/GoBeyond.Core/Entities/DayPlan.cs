namespace GoBeyond.Core.Entities;

public class DayPlan : BaseEntity
{
    public int TrainingPlanId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan TrainingDuration { get; set; }
    public string TrainingDescription { get; set; } = string.Empty;
    public TimeSpan NutritionDuration { get; set; }
    public string NutritionDescription { get; set; } = string.Empty;

    public TrainingPlan TrainingPlan { get; set; } = null!;
}
