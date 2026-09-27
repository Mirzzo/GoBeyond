using GoBeyond.Core.Enums;

namespace GoBeyond.Core.DTOs.Profile;

public class UserProfileDto
{
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public int GenderId { get; set; }
    public string GenderName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? ProfileImageUrl { get; set; }
    public MentorProfileInfoDto? Mentor { get; set; }
    public ClientProfileInfoDto? Client { get; set; }
}

public sealed class MentorProfileInfoDto
{
    public int TrainingTypeId { get; set; }
    public string TrainingTypeName { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string Bio { get; set; } = string.Empty;
    public int YearsOfExperience { get; set; }
    public decimal MonthlyPrice { get; set; }
    public List<int> SpecializationIds { get; set; } = [];
    public List<string> SpecializationNames { get; set; } = [];
    public MentorApprovalStatus Status { get; set; }
}

public sealed class ClientProfileInfoDto
{
    public decimal WeightKg { get; set; }
    public decimal HeightCm { get; set; }
    public int FitnessLevelId { get; set; }
    public string FitnessLevelName { get; set; } = string.Empty;
    public int TrainingExperienceYears { get; set; }
    public int FitnessGoalId { get; set; }
    public string FitnessGoalName { get; set; } = string.Empty;
    public string? GoalDescription { get; set; }
    public int? PreferredTrainingTypeId { get; set; }
    public string? PreferredTrainingTypeName { get; set; }
}

public sealed record ProfileImageResponse(string? ProfileImageUrl);
