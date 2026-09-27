using System.ComponentModel.DataAnnotations;
using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Profile;
using GoBeyond.Infrastructure.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[ApiController]
[Route("api/user-profile")]
[Authorize]
public sealed class UserProfileController(IUserProfileService profileService) : ControllerBase
{
    [HttpGet("me")]
    public Task<UserProfileDto> GetMe(CancellationToken cancellationToken) =>
        profileService.GetMeAsync(User.GetUserId(), cancellationToken);

    [HttpPut("me")]
    public Task<UserProfileDto> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken) =>
        profileService.UpdateMeAsync(User.GetUserId(), request, cancellationToken);

    [HttpPost("me/photo")]
    [Consumes("multipart/form-data")]
    public Task<ProfileImageResponse> UploadPhoto(
        [Required(ErrorMessage = "Odaberite sliku (JPG ili PNG, najviše 5 MB).")] IFormFile file, CancellationToken cancellationToken) =>
        profileService.UploadPhotoAsync(User.GetUserId(), file.ToFileUpload(), cancellationToken);

    [HttpDelete("me/photo")]
    public async Task<IActionResult> DeletePhoto(CancellationToken cancellationToken)
    {
        await profileService.DeletePhotoAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }
}
