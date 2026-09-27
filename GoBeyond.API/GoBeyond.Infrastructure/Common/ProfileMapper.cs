using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.DTOs.Profile;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Validation;

namespace GoBeyond.Infrastructure.Common;

/// <summary>Mapiranje korisnika i profila u DTO objekte (koriste ga profil, admin i auth servisi).</summary>
public static class ProfileMapper
{
    public static int Age(DateOnly dateOfBirth) =>
        AgeCalculator.From(dateOfBirth, DateOnly.FromDateTime(DateTime.UtcNow));

    /// <summary>Očekuje učitane: Gender, MentorProfile (TrainingType, Specializations.FitnessGoal), ClientProfile (FitnessLevel, FitnessGoal, PreferredTrainingType).</summary>
    public static UserProfileDto ToUserProfile(User user) => new()
    {
        Username = user.Username,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber,
        DateOfBirth = user.DateOfBirth,
        GenderId = user.GenderId,
        GenderName = user.Gender.Name,
        Role = user.Role,
        ProfileImageUrl = user.ProfileImageUrl,
        Mentor = user.Role == Core.Enums.UserRole.Mentor && user.MentorProfile is not null ? ToMentorInfo(user.MentorProfile) : null,
        Client = user.Role == Core.Enums.UserRole.Client && user.ClientProfile is not null ? ToClientInfo(user.ClientProfile) : null
    };

    public static MentorProfileInfoDto ToMentorInfo(MentorProfile mentor) => new()
    {
        TrainingTypeId = mentor.TrainingTypeId,
        TrainingTypeName = mentor.TrainingType.Name,
        Nickname = mentor.Nickname,
        Bio = mentor.Bio,
        YearsOfExperience = mentor.YearsOfExperience,
        MonthlyPrice = mentor.MonthlyPrice,
        SpecializationIds = mentor.Specializations.Select(x => x.FitnessGoalId).OrderBy(x => x).ToList(),
        SpecializationNames = mentor.Specializations.Select(x => x.FitnessGoal.Name).OrderBy(x => x).ToList(),
        Status = mentor.Status
    };

    public static ClientProfileInfoDto ToClientInfo(ClientProfile client) => new()
    {
        WeightKg = client.WeightKg,
        HeightCm = client.HeightCm,
        FitnessLevelId = client.FitnessLevelId,
        FitnessLevelName = client.FitnessLevel.Name,
        TrainingExperienceYears = client.TrainingExperienceYears,
        FitnessGoalId = client.FitnessGoalId,
        FitnessGoalName = client.FitnessGoal.Name,
        GoalDescription = client.GoalDescription,
        PreferredTrainingTypeId = client.PreferredTrainingTypeId,
        PreferredTrainingTypeName = client.PreferredTrainingType?.Name
    };

    public static AdminUserDto ToAdminUser(User user) => FillAdminUser(new AdminUserDto(), user);

    public static AdminUserDetailDto ToAdminUserDetail(User user)
    {
        var dto = FillAdminUser(new AdminUserDetailDto(), user);
        dto.DateOfBirth = user.DateOfBirth;
        dto.GenderId = user.GenderId;
        dto.GenderName = user.Gender.Name;
        dto.Mentor = user.MentorProfile is not null ? ToMentorInfo(user.MentorProfile) : null;
        dto.Client = user.ClientProfile is not null ? ToClientInfo(user.ClientProfile) : null;
        return dto;
    }

    private static T FillAdminUser<T>(T dto, User user) where T : AdminUserDto
    {
        dto.Id = user.Id;
        dto.Username = user.Username;
        dto.FirstName = user.FirstName;
        dto.LastName = user.LastName;
        dto.FullName = user.FullName;
        dto.Email = user.Email;
        dto.PhoneNumber = user.PhoneNumber;
        dto.Role = user.Role;
        dto.IsActive = user.IsActive;
        dto.CreatedAt = user.CreatedAt;
        dto.ProfileImageUrl = user.ProfileImageUrl;
        return dto;
    }

    public static CertificateDto ToCertificate(MentorCertificate certificate) => new()
    {
        Id = certificate.Id,
        FileName = certificate.FileName,
        FileUrl = certificate.FileUrl,
        UploadedAt = certificate.UploadedAt,
        IsVerified = certificate.IsVerified
    };
}
