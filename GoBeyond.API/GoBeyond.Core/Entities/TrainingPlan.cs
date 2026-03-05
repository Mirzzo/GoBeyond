using GoBeyond.Core.Enums;

namespace GoBeyond.Core.Entities;

public class TrainingPlan : BaseEntity
{
    public int SubscriptionId { get; set; }
    public int MentorProfileId { get; set; }
    public int ClientProfileId { get; set; }
    public string MotivationalQuote { get; set; } = string.Empty;
    public int WeekNumber { get; set; }
    public TrainingPlanStatus Status { get; set; } = TrainingPlanStatus.Draft;

    public Subscription Subscription { get; set; } = null!;
    public MentorProfile MentorProfile { get; set; } = null!;
    public ClientProfile ClientProfile { get; set; } = null!;
    public ICollection<DayPlan> DayPlans { get; set; } = new List<DayPlan>();
}
