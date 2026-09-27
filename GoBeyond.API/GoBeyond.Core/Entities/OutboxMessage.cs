namespace GoBeyond.Core.Entities;

/// <summary>Email koji čeka objavu na RabbitMQ (transactional outbox).</summary>
public class OutboxMessage : BaseEntity
{
    public int? UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }

    /// <summary>Postavlja se nakon maksimalnog broja neuspjelih pokušaja; poruka se više ne šalje.</summary>
    public DateTime? FailedAt { get; set; }

    public User? User { get; set; }
}
