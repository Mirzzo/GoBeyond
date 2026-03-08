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
                    FitnessLevel = clientRequest.FitnessLevel.Trim()
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
                user.MentorProfile.Bio = mentorRequest.Bio.Trim();
                user.MentorProfile.Age = mentorRequest.Age;
                user.MentorProfile.Category = mentorRequest.Category;
                user.MentorProfile.Price = mentorRequest.Price;
                break;

            case UserRole.Client:
                var clientRequest = request.ClientProfile
                    ?? throw new InvalidOperationException("Client profile payload is required.");

                user.ClientProfile ??= new ClientProfile();
                user.ClientProfile.Weight = clientRequest.Weight;
                user.ClientProfile.Height = clientRequest.Height;
                user.ClientProfile.Age = clientRequest.Age;
                user.ClientProfile.FitnessLevel = clientRequest.FitnessLevel.Trim();
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

    private async Task EnsureUniqueEmailAsync(int userId, string normalizedEmail, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Users
            .AnyAsync(x => x.Id != userId && x.Email == normalizedEmail, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Email is already registered.");
        }
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
                user.ClientProfile.FitnessLevel);
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
