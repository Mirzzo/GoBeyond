using GoBeyond.Core.DTOs.Profile;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.Files;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Files;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Users;

public interface IUserProfileService
{
    Task<UserProfileDto> GetMeAsync(int userId, CancellationToken cancellationToken = default);
    Task<UserProfileDto> UpdateMeAsync(int userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<ProfileImageResponse> UploadPhotoAsync(int userId, FileUpload file, CancellationToken cancellationToken = default);
    Task DeletePhotoAsync(int userId, CancellationToken cancellationToken = default);
}

public sealed class UserProfileService(
    GoBeyondDbContext db,
    IUserAccountValidator accountValidator,
    IFileStorageService files) : IUserProfileService
{
    public async Task<UserProfileDto> GetMeAsync(int userId, CancellationToken cancellationToken = default) =>
        ProfileMapper.ToUserProfile(await LoadAsync(userId, cancellationToken));

    public async Task<UserProfileDto> UpdateMeAsync(int userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await LoadAsync(userId, cancellationToken);

        var errors = new ValidationErrorCollector();
        await accountValidator.ValidateAccountAsync(errors, request, userId, cancellationToken);
        if (user.Role == UserRole.Mentor)
        {
            if (request.Mentor is null)
                errors.Add("mentor", "Unesite mentorske podatke (vrsta treninga, biografija, iskustvo, cijena i specijalizacije).");
            else
                await accountValidator.ValidateMentorAsync(errors, request.Mentor.TrainingTypeId, request.Mentor.SpecializationIds, "mentor.", cancellationToken);
        }
        if (user.Role == UserRole.Client)
        {
            if (request.Client is null)
                errors.Add("client", "Unesite klijentske podatke (težina, visina, nivo spreme, iskustvo i cilj).");
            else
                await accountValidator.ValidateClientAsync(errors, request.Client.FitnessLevelId, request.Client.FitnessGoalId,
                    request.Client.PreferredTrainingTypeId, "client.", cancellationToken);
        }
        errors.ThrowIfAny();

        ProfileUpdater.ApplyAccount(user, request);
        if (user.Role == UserRole.Mentor && user.MentorProfile is not null)
            ProfileUpdater.ApplyMentor(user.MentorProfile, request.Mentor!);
        if (user.Role == UserRole.Client && user.ClientProfile is not null)
            ProfileUpdater.ApplyClient(user.ClientProfile, request.Client!);

        await db.SaveChangesAsync(cancellationToken);
        return await GetMeAsync(userId, cancellationToken);
    }

    public async Task<ProfileImageResponse> UploadPhotoAsync(int userId, FileUpload file, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
                   ?? throw new NotFoundException(DomainTexts.UserNotFound);

        var url = await files.SaveAsync(file, "profile", UploadKind.Image, "file", cancellationToken);
        var previous = user.ProfileImageUrl;
        user.ProfileImageUrl = url;
        await db.SaveChangesAsync(cancellationToken);
        files.Delete(previous);
        return new ProfileImageResponse(url);
    }

    public async Task DeletePhotoAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
                   ?? throw new NotFoundException(DomainTexts.UserNotFound);
        var previous = user.ProfileImageUrl;
        user.ProfileImageUrl = null;
        await db.SaveChangesAsync(cancellationToken);
        files.Delete(previous);
    }

    private async Task<User> LoadAsync(int userId, CancellationToken cancellationToken) =>
        await db.Users.WithProfile().AsSplitQuery().FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(DomainTexts.UserNotFound);
}
