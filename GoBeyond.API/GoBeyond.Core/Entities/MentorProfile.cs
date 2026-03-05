using GoBeyond.Core.Enums;

namespace GoBeyond.Core.Entities;

public class MentorProfile : BaseEntity
{
    public int UserId { get; set; }
    public string Bio { get; set; } = string.Empty;
    public int Age { get; set; }
    public MentorCategory Category { get; set; }
    public decimal Price { get; set; }
    public MentorApprovalStatus Status { get; set; } = MentorApprovalStatus.Pending;
    public string? StripeAccountId { get; set; }

    public User User { get; set; } = null!;
    public ICollection<MentorCertificate> Certificates { get; set; } = new List<MentorCertificate>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<TrainingPlan> TrainingPlans { get; set; } = new List<TrainingPlan>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
