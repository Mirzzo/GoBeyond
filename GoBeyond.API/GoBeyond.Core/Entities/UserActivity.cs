namespace GoBeyond.Core.Entities;

/// <summary>Vrijeme provedeno na platformi po korisniku i danu (puni se heartbeat pozivima).</summary>
public class UserActivity : BaseEntity
{
    public int UserId { get; set; }
    public DateOnly Day { get; set; }
    public int ActiveSeconds { get; set; }
    public DateTime LastHeartbeatAt { get; set; }

    public User User { get; set; } = null!;
}
