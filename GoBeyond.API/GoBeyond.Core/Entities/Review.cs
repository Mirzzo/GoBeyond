namespace GoBeyond.Core.Entities;

public class Review : BaseEntity
{
    public int SubscriptionId { get; set; }
    public int ClientProfileId { get; set; }
    public int MentorProfileId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Subscription Subscription { get; set; } = null!;
    public ClientProfile ClientProfile { get; set; } = null!;
    public MentorProfile MentorProfile { get; set; } = null!;
}
