namespace GoBeyond.Core.Entities;

public class RefreshToken : BaseEntity
{
    public int UserId { get; set; }

    /// <summary>SHA-256 hash tokena (sam token se nikad ne čuva u bazi).</summary>
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public User User { get; set; } = null!;
}
