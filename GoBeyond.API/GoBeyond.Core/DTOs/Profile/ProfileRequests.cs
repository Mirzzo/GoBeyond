using System.ComponentModel.DataAnnotations;
using GoBeyond.Core.Validation;

namespace GoBeyond.Core.DTOs.Profile;

/// <summary>Osnovni podaci korisničkog računa (registracija, uređivanje profila, admin uređivanje).</summary>
public class AccountFieldsRequest
{
    [Required(ErrorMessage = "Ime je obavezno.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Ime mora imati 2–50 znakova.")]
    [RegularExpression(ValidationPatterns.Name, ErrorMessage = "Ime smije sadržavati samo slova, razmak, apostrof i crticu (2–50 znakova).")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Prezime je obavezno.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Prezime mora imati 2–50 znakova.")]
    [RegularExpression(ValidationPatterns.Name, ErrorMessage = "Prezime smije sadržavati samo slova, razmak, apostrof i crticu (2–50 znakova).")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Korisničko ime je obavezno.")]
    [RegularExpression(ValidationPatterns.Username, ErrorMessage = ValidationPatterns.UsernameMessage)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email je obavezan.")]
    [StringLength(100, ErrorMessage = "Email može imati najviše 100 znakova.")]
    [RegularExpression(ValidationPatterns.Email, ErrorMessage = ValidationPatterns.EmailMessage)]
    public string Email { get; set; } = string.Empty;

    [RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneMessage)]
    public string? PhoneNumber { get; set; }

    [DateOfBirth(16, 100)]
    public DateOnly DateOfBirth { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Odaberite spol.")]
    public int GenderId { get; set; }
}

public sealed class MentorProfileRequest
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

public sealed class ClientProfileRequest
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

/// <summary>PUT /api/user-profile/me</summary>
public sealed class UpdateProfileRequest : AccountFieldsRequest
{
    public MentorProfileRequest? Mentor { get; set; }
    public ClientProfileRequest? Client { get; set; }
}
