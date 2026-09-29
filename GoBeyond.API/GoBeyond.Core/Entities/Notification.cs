using GoBeyond.Core.Enums;

namespace GoBeyond.Core.Entities;

public class Notification : BaseEntity
{
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? AnnouncementId { get; set; }

    /// <summary>
    /// Pošiljalac poruke za NewMessage obavijest (jedna nepročitana po pošiljaocu). Null za ostale tipove i za NewMessage
    /// obavijesti nastale prije ove kolone, koje se prepoznaju po imenu u naslovu.
    /// </summary>
    public int? SenderUserId { get; set; }

    public User User { get; set; } = null!;
    public Announcement? Announcement { get; set; }
}
