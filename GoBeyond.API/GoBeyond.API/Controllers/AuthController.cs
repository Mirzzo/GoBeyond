using GoBeyond.API.Extensions;
using GoBeyond.API.Models;
using GoBeyond.Core.DTOs.Auth;
using GoBeyond.Core.DTOs.Common;
using GoBeyond.Infrastructure.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Prijava korisničkim imenom ILI email adresom.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public Task<AuthResponse> Login([FromBody] LoginRequest request, CancellationToken cancellationToken) =>
        authService.LoginAsync(request, cancellationToken);

    [HttpPost("register/client")]
    [AllowAnonymous]
    public Task<AuthResponse> RegisterClient([FromBody] RegisterClientRequest request, CancellationToken cancellationToken) =>
        authService.RegisterClientAsync(request, cancellationToken);

    /// <summary>multipart/form-data: podaci mentora + 1–5 certifikata (pdf/jpg/jpeg/png, ≤ 5 MB).</summary>
    [HttpPost("register/mentor")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    public Task<MessageResponse> RegisterMentor([FromForm] RegisterMentorForm form, CancellationToken cancellationToken) =>
        authService.RegisterMentorAsync(form, form.Certificates.ToFileUploads(), cancellationToken);

    [HttpPost("refresh")]
    [AllowAnonymous]
    public Task<AuthResponse> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken) =>
        authService.RefreshAsync(request.RefreshToken, cancellationToken);

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(User.GetUserId(), request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public Task<MessageResponse> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken) =>
        authService.ChangePasswordAsync(User.GetUserId(), request, cancellationToken);
}
