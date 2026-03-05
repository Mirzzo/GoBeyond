using GoBeyond.Core.DTOs.Auth;

namespace GoBeyond.Infrastructure.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterClientAsync(RegisterClientRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> RegisterMentorAsync(RegisterMentorRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}
