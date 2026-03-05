using GoBeyond.Core.DTOs.Auth;
using GoBeyond.API.Extensions;
using GoBeyond.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register/client")]
    public Task<AuthResponseDto> RegisterClient([FromBody] RegisterClientRequestDto request, CancellationToken cancellationToken)
        => authService.RegisterClientAsync(request, cancellationToken);

    [AllowAnonymous]
    [HttpPost("register/mentor")]
    public Task<AuthResponseDto> RegisterMentor([FromBody] RegisterMentorRequestDto request, CancellationToken cancellationToken)
        => authService.RegisterMentorAsync(request, cancellationToken);

    [AllowAnonymous]
    [HttpPost("login")]
    public Task<AuthResponseDto> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
        => authService.LoginAsync(request, cancellationToken);

    [AllowAnonymous]
    [HttpPost("refresh")]
    public Task<AuthResponseDto> Refresh([FromBody] RefreshRequestDto request, CancellationToken cancellationToken)
        => authService.RefreshAsync(request.RefreshToken, cancellationToken);

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken);
        return NoContent();
    }
}
