using GoBeyond.Core.Enums;

namespace GoBeyond.Core.Entities;

public class Subscription : BaseEntity
{
    public int ClientProfileId { get; set; }
    public int MentorProfileId { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal AmountPaid { get; set; }
    public string? StripePaymentIntentId { get; set; }

    public ClientProfile ClientProfile { get; set; } = null!;
    public MentorProfile MentorProfile { get; set; } = null!;
    public Questionnaire? Questionnaire { get; set; }
    public ICollection<TrainingPlan> TrainingPlans { get; set; } = new List<TrainingPlan>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
