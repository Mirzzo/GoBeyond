using GoBeyond.Core.Enums;

namespace GoBeyond.Core.Entities;

public class User : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public int GenderId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? ProfileImageUrl { get; set; }

    /// <summary>false = korisnik je blokiran.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Soft delete. Obrisan korisnik se ne može odblokirati.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Upisuje se u access i refresh token. Nova vrijednost (promjena/reset lozinke, blokiranje, brisanje, promjena uloge)
    /// odmah poništava ranije izdate tokene, i nakon odblokiranja ili vraćanja uloge (izuzetak: sesija koja je sama
    /// promijenila lozinku, vidi UserSessions).
    /// </summary>
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public Gender Gender { get; set; } = null!;
    public MentorProfile? MentorProfile { get; set; }
    public ClientProfile? ClientProfile { get; set; }
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<UserActivity> Activities { get; set; } = new List<UserActivity>();

    public string FullName => $"{FirstName} {LastName}";
}
