using GoBeyond.Core.Enums;

namespace GoBeyond.Core.Entities;

public class Subscription : BaseEntity
{
    public int ClientProfileId { get; set; }
    public int MentorProfileId { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.PendingPayment;
    public decimal Price { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? StatusReason { get; set; }

    /// <summary>Kada je klijentu poslan podsjetnik o skorom isteku (resetuje se pri obnovi).</summary>
    public DateTime? ExpiryReminderSentAt { get; set; }

    /// <summary>Kada je mentoru zadnji put poslana obavijest o izostanku plana.</summary>
    public DateTime? PlanMissingReminderSentAt { get; set; }

    public ClientProfile ClientProfile { get; set; } = null!;
    public MentorProfile MentorProfile { get; set; } = null!;
    public Questionnaire? Questionnaire { get; set; }
    public TrainingPlan? TrainingPlan { get; set; }
    public Review? Review { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
