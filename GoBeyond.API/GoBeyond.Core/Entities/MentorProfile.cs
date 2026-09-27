using GoBeyond.Core.Enums;

namespace GoBeyond.Core.Entities;

public class MentorProfile : BaseEntity
{
    public int UserId { get; set; }
    public int TrainingTypeId { get; set; }

    /// <summary>Nadimak ("AKA").</summary>
    public string? Nickname { get; set; }
    public string Bio { get; set; } = string.Empty;
    public int YearsOfExperience { get; set; }
    public decimal MonthlyPrice { get; set; }
    public MentorApprovalStatus Status { get; set; } = MentorApprovalStatus.Pending;
    public string? RejectionReason { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public User User { get; set; } = null!;
    public TrainingType TrainingType { get; set; } = null!;
    public ICollection<MentorSpecialization> Specializations { get; set; } = new List<MentorSpecialization>();
    public ICollection<MentorCertificate> Certificates { get; set; } = new List<MentorCertificate>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<TrainingPlan> TrainingPlans { get; set; } = new List<TrainingPlan>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
