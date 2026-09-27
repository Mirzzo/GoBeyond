using System.ComponentModel.DataAnnotations;
using GoBeyond.Core.Enums;

namespace GoBeyond.Core.DTOs.Communication;

public sealed class NotificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class MessageThreadDto
{
    public int SubscriptionId { get; set; }
    public string OtherPartyName { get; set; } = string.Empty;
    public string? OtherPartyPhotoUrl { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
    public bool CanSend { get; set; }
}

public sealed class MessageDto
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsMine { get; set; }
    public string SenderName { get; set; } = string.Empty;
}

public sealed class SendMessageRequest
{
    [Required(ErrorMessage = "Poruka ne smije biti prazna.")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Poruka mora imati 1–2000 znakova.")]
    public string Content { get; set; } = string.Empty;
}

public sealed class CreateReviewRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Odaberite saradnju koju ocjenjujete.")]
    public int SubscriptionId { get; set; }

    [Range(1, 5, ErrorMessage = "Ocjena mora biti cijeli broj od 1 do 5.")]
    public int Rating { get; set; }

    [Required(ErrorMessage = "Komentar je obavezan.")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Komentar mora imati 10–1000 znakova.")]
    public string Comment { get; set; } = string.Empty;
}

public sealed class UpdateReviewRequest
{
    [Range(1, 5, ErrorMessage = "Ocjena mora biti cijeli broj od 1 do 5.")]
    public int Rating { get; set; }

    [Required(ErrorMessage = "Komentar je obavezan.")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Komentar mora imati 10–1000 znakova.")]
    public string Comment { get; set; } = string.Empty;
}
