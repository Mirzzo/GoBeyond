using GoBeyond.Core.Enums;

namespace GoBeyond.Core.Entities;

public class Notification : BaseEntity
{
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }

    public User User { get; set; } = null!;
}
