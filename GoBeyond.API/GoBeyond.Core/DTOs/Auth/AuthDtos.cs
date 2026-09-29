using System.ComponentModel.DataAnnotations;
using GoBeyond.Core.DTOs.Profile;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Validation;

namespace GoBeyond.Core.DTOs.Auth;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Unesite korisničko ime ili email.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Unesite lozinku.")]
    public string Password { get; set; } = string.Empty;
}

public abstract class RegisterRequestBase : AccountFieldsRequest
{
    [Required(ErrorMessage = "Lozinka je obavezna.")]
    [Password]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Potvrdite lozinku.")]
    [Compare(nameof(Password), ErrorMessage = "Lozinke se ne podudaraju.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class RegisterClientRequest : RegisterRequestBase
{
    [Range(typeof(decimal), "30", "300", ErrorMessage = "Težina mora biti između 30 i 300 kg.")]
    public decimal WeightKg { get; set; }

    [Range(typeof(decimal), "100", "250", ErrorMessage = "Visina mora biti između 100 i 250 cm.")]
    public decimal HeightCm { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Odaberite nivo fizičke spreme.")]
    public int FitnessLevelId { get; set; }

    [Range(0, 60, ErrorMessage = "Iskustvo u treniranju mora biti cijeli broj između 0 i 60 godina.")]
    public int TrainingExperienceYears { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Odaberite fitness cilj.")]
    public int FitnessGoalId { get; set; }

    [StringLength(500, ErrorMessage = "Opis cilja može imati najviše 500 znakova.")]
    public string? GoalDescription { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Odaberite validnu vrstu treninga.")]
    public int? PreferredTrainingTypeId { get; set; }
}

/// <summary>Polja mentorske registracije (multipart/form-data; certifikati se šalju odvojeno kao fajlovi).</summary>
public class RegisterMentorRequest : RegisterRequestBase
{
    private string _bio = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Odaberite vrstu treninga.")]
    public int TrainingTypeId { get; set; }

    [StringLength(50, ErrorMessage = "Nadimak može imati najviše 50 znakova.")]
    public string? Nickname { get; set; }

    [Required(ErrorMessage = "Biografija je obavezna.")]
    [StringLength(4000, MinimumLength = 50, ErrorMessage = "Biografija mora imati 50–4000 znakova.")]
    public string Bio
    {
        get => _bio;
        set => _bio = value?.Trim() ?? string.Empty;
    }

    [Range(0, 60, ErrorMessage = "Godine iskustva moraju biti cijeli broj između 0 i 60.")]
    public int YearsOfExperience { get; set; }

    [Range(typeof(decimal), "1", "1000", ErrorMessage = "Mjesečna cijena mora biti između 1 i 1000.")]
    [MaxDecimalPlaces(2, ErrorMessage = "Mjesečna cijena može imati najviše dvije decimale.")]
    public decimal MonthlyPrice { get; set; }

    [ItemCount(1, 10, ErrorMessage = "Odaberite najmanje jednu, a najviše 10 specijalizacija.")]
    public List<int> SpecializationIds { get; set; } = [];
}

public sealed class RefreshTokenRequest
{
    [Required(ErrorMessage = "Refresh token je obavezan.")]
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required(ErrorMessage = "Unesite trenutnu lozinku.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nova lozinka je obavezna.")]
    [Password]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Potvrdite novu lozinku.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Lozinke se ne podudaraju.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [Required(ErrorMessage = "Nova lozinka je obavezna.")]
    [Password]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Potvrdite novu lozinku.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Lozinke se ne podudaraju.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public AuthUserDto User { get; set; } = new();
}

public sealed class AuthUserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? ProfileImageUrl { get; set; }
}
