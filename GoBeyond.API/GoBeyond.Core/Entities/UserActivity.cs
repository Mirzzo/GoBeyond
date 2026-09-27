namespace GoBeyond.Core.Entities;

public class UserActivity : BaseEntity
{
    public int UserId { get; set; }
    public DateTime Day { get; set; }
    public int ActiveSeconds { get; set; }
    public DateTime LastHeartbeatAt { get; set; }
    public User User { get; set; } = null!;
}
