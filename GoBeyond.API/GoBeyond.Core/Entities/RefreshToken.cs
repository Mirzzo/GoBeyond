namespace GoBeyond.Core.Entities;

public class RefreshToken : BaseEntity
{
    public int UserId { get; set; }

    /// <summary>SHA-256 hash tokena (sam token se nikad ne čuva u bazi).</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Sesija (jedna prijava na jednom uređaju): novi token pri refresh-u je nasljeđuje, a access token je nosi kao "sid".</summary>
    public Guid SessionId { get; set; }

    /// <summary>User.SecurityStamp pri izdavanju; token važi samo dok je stamp korisnika isti.</summary>
    public Guid SecurityStamp { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public User User { get; set; } = null!;
}
