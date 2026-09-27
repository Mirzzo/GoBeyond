namespace GoBeyond.Core.Entities;

public class OutboxMessage : BaseEntity
{
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
