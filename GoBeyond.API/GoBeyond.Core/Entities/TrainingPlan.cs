using GoBeyond.Core.Enums;

namespace GoBeyond.Core.Entities;

public class TrainingPlan : BaseEntity
{
    public int SubscriptionId { get; set; }
    public int MentorProfileId { get; set; }
    public int ClientProfileId { get; set; }
    public string? MotivationalQuote { get; set; }
    public TrainingPlanStatus Status { get; set; } = TrainingPlanStatus.Draft;
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }

    /// <summary>Zadnja PlanUpdated obavijest klijentu (throttling).</summary>
    public DateTime? LastUpdateNotifiedAt { get; set; }

    public Subscription Subscription { get; set; } = null!;
    public MentorProfile MentorProfile { get; set; } = null!;
    public ClientProfile ClientProfile { get; set; } = null!;
    public ICollection<DayPlan> Days { get; set; } = new List<DayPlan>();
    public ICollection<TrainingSession> Sessions { get; set; } = new List<TrainingSession>();
}
