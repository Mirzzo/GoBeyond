using System.ComponentModel.DataAnnotations;
using GoBeyond.Core.DTOs.Profile;
using GoBeyond.Core.Enums;

namespace GoBeyond.Core.DTOs.Admin;

public class AdminUserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ProfileImageUrl { get; set; }
}

public sealed class AdminUserDetailDto : AdminUserDto
{
    public DateOnly DateOfBirth { get; set; }
    public int GenderId { get; set; }
    public string GenderName { get; set; } = string.Empty;
    public MentorProfileInfoDto? Mentor { get; set; }
    public ClientProfileInfoDto? Client { get; set; }
}

/// <summary>PUT /api/admin/users/{id} - admin ne unosi lozinku.</summary>
public sealed class AdminUpdateUserRequest : AccountFieldsRequest
{
    [EnumDataType(typeof(UserRole), ErrorMessage = "Odaberite validnu ulogu (Admin, Mentor ili Client).")]
    public UserRole Role { get; set; }

    public MentorProfileRequest? Mentor { get; set; }
    public ClientProfileRequest? Client { get; set; }
}

public sealed class AdminMentorDto
{
    public int UserId { get; set; }
    public int MentorProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string TrainingTypeName { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int ActiveSubscribers { get; set; }
    public bool IsActive { get; set; }
}

public class MentorRequestDto
{
    public int MentorProfileId { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TrainingTypeName { get; set; } = string.Empty;
    public int YearsOfExperience { get; set; }
    public DateTime RequestedAt { get; set; }
    public int CertificateCount { get; set; }
}

public sealed class MentorRequestDetailDto : MentorRequestDto
{
    public string? Nickname { get; set; }
    public string Bio { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public int Age { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal MonthlyPrice { get; set; }
    public List<string> SpecializationNames { get; set; } = [];
    public string? ProfileImageUrl { get; set; }
    public List<CertificateDto> Certificates { get; set; } = [];
}

public sealed class CertificateDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public bool IsVerified { get; set; }
}

public sealed class RejectRequest
{
    private string _reason = string.Empty;

    [Required(ErrorMessage = "Razlog je obavezan.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Razlog mora imati 10–500 znakova.")]
    public string Reason
    {
        get => _reason;
        set => _reason = value?.Trim() ?? string.Empty;
    }
}

public sealed class AdminClientDto
{
    public int UserId { get; set; }
    public int ClientProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string FitnessGoalName { get; set; } = string.Empty;
    public string FitnessLevelName { get; set; } = string.Empty;
    public string? ActiveMentorName { get; set; }
    public bool IsActive { get; set; }
}

public sealed class AdminSubscriptionDto
{
    public int Id { get; set; }
    public string ClientFullName { get; set; } = string.Empty;
    public string MentorFullName { get; set; } = string.Empty;
    public string TrainingTypeName { get; set; } = string.Empty;
    public SubscriptionStatus Status { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? StatusReason { get; set; }
}

public sealed class CancelSubscriptionRequest
{
    private string _reason = string.Empty;

    [Required(ErrorMessage = "Razlog je obavezan.")]
    [StringLength(300, MinimumLength = 5, ErrorMessage = "Razlog mora imati 5–300 znakova.")]
    public string Reason
    {
        get => _reason;
        set => _reason = value?.Trim() ?? string.Empty;
    }
}

public sealed class AnnouncementDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public UserRole? TargetRole { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public int RecipientCount { get; set; }
}

public class UpdateAnnouncementRequest
{
    private string _title = string.Empty;
    private string _content = string.Empty;

    [Required(ErrorMessage = "Naslov je obavezan.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "Naslov mora imati 3–120 znakova.")]
    public string Title
    {
        get => _title;
        set => _title = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Sadržaj je obavezan.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Sadržaj mora imati 10–2000 znakova.")]
    public string Content
    {
        get => _content;
        set => _content = value?.Trim() ?? string.Empty;
    }
}

public sealed class CreateAnnouncementRequest : UpdateAnnouncementRequest
{
    /// <summary>null = svi korisnici.</summary>
    [EnumDataType(typeof(UserRole), ErrorMessage = "Odaberite validnu ciljnu ulogu (Admin, Mentor ili Client) ili ostavite prazno za sve.")]
    public UserRole? TargetRole { get; set; }
}
