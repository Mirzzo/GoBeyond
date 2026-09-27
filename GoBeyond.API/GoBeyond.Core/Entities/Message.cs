namespace GoBeyond.Core.Entities;

/// <summary>Interna poruka mentor - klijent u okviru jedne pretplate.</summary>
public class Message : BaseEntity
{
    public int SubscriptionId { get; set; }
    public int SenderUserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }

    public Subscription Subscription { get; set; } = null!;
    public User SenderUser { get; set; } = null!;
}
