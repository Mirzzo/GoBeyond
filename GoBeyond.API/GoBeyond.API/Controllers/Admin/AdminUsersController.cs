using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.DTOs.Auth;
using GoBeyond.Core.DTOs.Common;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminUsersController(IAdminUserService userService) : ControllerBase
{
    [HttpGet]
    public Task<List<AdminUserDto>> Get([FromQuery] AdminUserSearchObject searchObject, CancellationToken cancellationToken) =>
        userService.GetUsersAsync(searchObject, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<AdminUserDetailDto> GetById(int id, CancellationToken cancellationToken) =>
        userService.GetUserAsync(id, cancellationToken);

    [HttpPut("{id:int}")]
    public Task<AdminUserDetailDto> Update(int id, [FromBody] AdminUpdateUserRequest request, CancellationToken cancellationToken) =>
        userService.UpdateUserAsync(User.GetUserId(), id, request, cancellationToken);

    /// <summary>Administrator postavlja novu lozinku bez unosa stare.</summary>
    [HttpPut("{id:int}/reset-password")]
    public Task<MessageResponse> ResetPassword(int id, [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken) =>
        userService.ResetPasswordAsync(id, request, cancellationToken);

    [HttpPut("{id:int}/block")]
    public Task<AdminUserDto> Block(int id, CancellationToken cancellationToken) =>
        userService.BlockAsync(User.GetUserId(), id, cancellationToken);

    [HttpPut("{id:int}/unblock")]
    public Task<AdminUserDto> Unblock(int id, CancellationToken cancellationToken) =>
        userService.UnblockAsync(id, cancellationToken);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await userService.DeleteAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }
}
