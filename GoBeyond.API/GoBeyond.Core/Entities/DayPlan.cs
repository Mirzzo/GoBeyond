namespace GoBeyond.Core.Entities;

public class DayPlan : BaseEntity
{
    public int TrainingPlanId { get; set; }

    /// <summary>1 = Ponedjeljak ... 7 = Nedjelja.</summary>
    public int DayOfWeek { get; set; }
    public int TrainingDurationMinutes { get; set; }
    public string TrainingDescription { get; set; } = string.Empty;
    public int? NutritionDurationMinutes { get; set; }
    public string NutritionDescription { get; set; } = string.Empty;

    public TrainingPlan TrainingPlan { get; set; } = null!;
}
