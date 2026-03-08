using GoBeyond.Core.Enums;

namespace GoBeyond.Core.DTOs.Profile;

public record UpsertUserProfileRequestDto(
    string FirstName,
    string LastName,
    string Email,
    string? ProfileImageUrl,
    UpsertMentorProfileRequestDto? MentorProfile,
    UpsertClientProfileRequestDto? ClientProfile);

public record UpsertMentorProfileRequestDto(
    string Bio,
    int Age,
    MentorCategory Category,
    decimal Price);

public record UpsertClientProfileRequestDto(
    decimal Weight,
    decimal Height,
    int Age,
    string FitnessLevel);
