using GoBeyond.Core.Enums;

namespace GoBeyond.Core.DTOs.Profile;

public record UserProfileDto(
    int UserId,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    bool IsActive,
    string? ProfileImageUrl,
    MentorProfileDto? MentorProfile,
    ClientProfileDto? ClientProfile);

public record MentorProfileDto(
    string Bio,
    int Age,
    MentorCategory Category,
    decimal Price,
    MentorApprovalStatus Status,
    string? StripeAccountId);

public record ClientProfileDto(
    decimal Weight,
    decimal Height,
    int Age,
    string FitnessLevel);
