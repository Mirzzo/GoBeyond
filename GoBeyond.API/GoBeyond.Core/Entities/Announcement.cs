using GoBeyond.Core.Enums;

namespace GoBeyond.Core.Entities;

/// <summary>Sistemska obavijest koju objavljuje administrator.</summary>
public class Announcement : BaseEntity
{
    public int CreatedByUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    /// <summary>null = svi korisnici.</summary>
    public UserRole? TargetRole { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
