using GoBeyond.Core.DTOs.Profile;

namespace GoBeyond.Infrastructure.Interfaces;

public interface IUserProfileService
{
    Task<UserProfileDto> GetMyProfileAsync(int userId, CancellationToken cancellationToken = default);
    Task<UserProfileDto> CreateMyProfileAsync(int userId, UpsertUserProfileRequestDto request, CancellationToken cancellationToken = default);
    Task<UserProfileDto> UpdateMyProfileAsync(int userId, UpsertUserProfileRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteMyProfileAsync(int userId, CancellationToken cancellationToken = default);
}
