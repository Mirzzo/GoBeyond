using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Profile;
using GoBeyond.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[Authorize]
[ApiController]
[Route("api/user-profile")]
public class UserProfileController(IUserProfileService userProfileService) : ControllerBase
{
    [HttpGet("me")]
    public Task<UserProfileDto> GetMyProfile(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        return userProfileService.GetMyProfileAsync(userId, cancellationToken);
    }

    [HttpPost("me")]
    public Task<UserProfileDto> CreateMyProfile([FromBody] UpsertUserProfileRequestDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        return userProfileService.CreateMyProfileAsync(userId, request, cancellationToken);
    }

    [HttpPut("me")]
    public Task<UserProfileDto> UpdateMyProfile([FromBody] UpsertUserProfileRequestDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        return userProfileService.UpdateMyProfileAsync(userId, request, cancellationToken);
    }

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMyProfile(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await userProfileService.DeleteMyProfileAsync(userId, cancellationToken);
        return NoContent();
    }
}
