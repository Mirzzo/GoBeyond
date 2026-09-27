using GoBeyond.Core.DTOs.Profile;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services;

public class UserProfileService(GoBeyondDbContext dbContext) : IUserProfileService
{
    public async Task<UserProfileDto> GetMyProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserWithProfilesAsync(userId, cancellationToken);
        return MapToDto(user);
    }

    public async Task<UserProfileDto> CreateMyProfileAsync(int userId, UpsertUserProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserWithProfilesAsync(userId, cancellationToken);
        ValidateProfileRequest(request, user.Role);

        ApplyBaseFields(user, request);
        await EnsureUniqueEmailAsync(user.Id, user.Email, cancellationToken);

        switch (user.Role)
        {
            case UserRole.Mentor:
                if (user.MentorProfile is not null)
                {
                    throw new InvalidOperationException("Mentor profile already exists.");
                }

                var mentorRequest = request.MentorProfile
                    ?? throw new InvalidOperationException("Mentor profile payload is required.");

                user.MentorProfile = new MentorProfile
                {
                    Bio = mentorRequest.Bio.Trim(),
                    Age = mentorRequest.Age,
                    Category = mentorRequest.Category,
                    Price = mentorRequest.Price,
                    Status = MentorApprovalStatus.Pending
                };
                user.MentorProfile.TrainingTypeId = await ResolveTrainingTypeIdAsync(mentorRequest.Category, cancellationToken);
                break;

            case UserRole.Client:
                if (user.ClientProfile is not null)
                {
                    throw new InvalidOperationException("Client profile already exists.");
                }

                var clientRequest = request.ClientProfile
                    ?? throw new InvalidOperationException("Client profile payload is required.");

                user.ClientProfile = new ClientProfile
                {
                    Weight = clientRequest.Weight,
                    Height = clientRequest.Height,
                    Age = clientRequest.Age,
                    FitnessLevel = clientRequest.FitnessLevel.Trim(),
                    Sex = clientRequest.Sex.Trim(),
                    TrainingExperience = clientRequest.TrainingExperience.Trim()
                };
                break;

            default:
                throw new InvalidOperationException("Create profile operation is not supported for this role.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(user);
    }

    public async Task<UserProfileDto> UpdateMyProfileAsync(int userId, UpsertUserProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserWithProfilesAsync(userId, cancellationToken);
        ValidateProfileRequest(request, user.Role);

        ApplyBaseFields(user, request);
        await EnsureUniqueEmailAsync(user.Id, user.Email, cancellationToken);

        switch (user.Role)
        {
            case UserRole.Mentor:
                var mentorRequest = request.MentorProfile
                    ?? throw new InvalidOperationException("Mentor profile payload is required.");

                user.MentorProfile ??= new MentorProfile
                {
                    Status = MentorApprovalStatus.Pending
                };
                var professionalDetailsChanged = user.MentorProfile.Bio != mentorRequest.Bio.Trim() ||
                    user.MentorProfile.Age != mentorRequest.Age ||
                    user.MentorProfile.Category != mentorRequest.Category ||
                    user.MentorProfile.Price != mentorRequest.Price;
                user.MentorProfile.Bio = mentorRequest.Bio.Trim();
                user.MentorProfile.Age = mentorRequest.Age;
                user.MentorProfile.Category = mentorRequest.Category;
                user.MentorProfile.TrainingTypeId = await ResolveTrainingTypeIdAsync(mentorRequest.Category, cancellationToken);
                user.MentorProfile.Price = mentorRequest.Price;
                if (professionalDetailsChanged) user.MentorProfile.Status = MentorApprovalStatus.Pending;
                break;

            case UserRole.Client:
                var clientRequest = request.ClientProfile
                    ?? throw new InvalidOperationException("Client profile payload is required.");

                user.ClientProfile ??= new ClientProfile();
                user.ClientProfile.Weight = clientRequest.Weight;
                user.ClientProfile.Height = clientRequest.Height;
                user.ClientProfile.Age = clientRequest.Age;
                user.ClientProfile.FitnessLevel = clientRequest.FitnessLevel.Trim();
                user.ClientProfile.Sex = clientRequest.Sex.Trim();
                user.ClientProfile.TrainingExperience = clientRequest.TrainingExperience.Trim();
                break;

            case UserRole.Admin:
                break;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(user);
    }

    public async Task DeleteMyProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserWithProfilesAsync(userId, cancellationToken);
        user.IsActive = false;

        var refreshTokens = await dbContext.RefreshTokens
            .Where(x => x.UserId == userId && !x.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in refreshTokens)
        {
            refreshToken.IsRevoked = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> GetUserWithProfilesAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(x => x.MentorProfile)
            .Include(x => x.ClientProfile)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        return user;
    }

    private static void ApplyBaseFields(User user, UpsertUserProfileRequestDto request)
    {
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = request.Email.Trim().ToLowerInvariant();
        user.ProfileImageUrl = string.IsNullOrWhiteSpace(request.ProfileImageUrl)
            ? null
            : request.ProfileImageUrl.Trim();
    }

    private static void ValidateProfileRequest(UpsertUserProfileRequestDto request, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Trim().Length < 2 ||
            string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Trim().Length < 2 ||
            string.IsNullOrWhiteSpace(request.Email) ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(request.Email))
            throw new InvalidOperationException("Profile requires first and last name (2+ characters) and a valid email address.");

        if (role == UserRole.Mentor)
        {
            var mentor = request.MentorProfile;
            if (mentor is null || string.IsNullOrWhiteSpace(mentor.Bio) || mentor.Bio.Trim().Length < 10 ||
                mentor.Age is < 18 or > 80 || mentor.Price is <= 0 or > 10000 || !Enum.IsDefined(mentor.Category))
                throw new InvalidOperationException("Mentor profile requires bio (10+ characters), age 18-80, valid category and positive price.");
        }
        if (role == UserRole.Client)
        {
            var client = request.ClientProfile;
            if (client is null || client.Age is < 18 or > 100 || client.Weight is <= 0 or > 1000 ||
                client.Height is <= 0 or > 300 || string.IsNullOrWhiteSpace(client.FitnessLevel) ||
                !new[] { "Male", "Female", "Other" }.Contains(client.Sex, StringComparer.OrdinalIgnoreCase) ||
                !new[] { "Beginner", "Intermediate", "Advanced" }.Contains(client.TrainingExperience, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Client profile requires age 18-100, valid weight/height, fitness level, sex and training experience.");
        }
    }

    private async Task EnsureUniqueEmailAsync(int userId, string normalizedEmail, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Users
            .AnyAsync(x => x.Id != userId && x.Email == normalizedEmail, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Email is already registered.");
        }
    }

    private async Task<int> ResolveTrainingTypeIdAsync(MentorCategory category, CancellationToken cancellationToken)
    {
        var id = await dbContext.TrainingTypes.Where(x => x.Name == category.ToString())
            .Select(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        return id > 0 ? id : throw new InvalidOperationException("Selected training type does not exist.");
    }

    private static UserProfileDto MapToDto(User user)
    {
        MentorProfileDto? mentorProfile = null;
        if (user.MentorProfile is not null)
        {
            mentorProfile = new MentorProfileDto(
                user.MentorProfile.Bio,
                user.MentorProfile.Age,
                user.MentorProfile.Category,
                user.MentorProfile.Price,
                user.MentorProfile.Status,
                user.MentorProfile.StripeAccountId);
        }

        ClientProfileDto? clientProfile = null;
        if (user.ClientProfile is not null)
        {
            clientProfile = new ClientProfileDto(
                user.ClientProfile.Weight,
                user.ClientProfile.Height,
                user.ClientProfile.Age,
                user.ClientProfile.FitnessLevel,
                user.ClientProfile.Sex,
                user.ClientProfile.TrainingExperience);
        }

        return new UserProfileDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email,
            user.Role,
            user.IsActive,
            user.ProfileImageUrl,
            mentorProfile,
            clientProfile);
    }
}
