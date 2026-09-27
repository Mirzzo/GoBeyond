using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Infrastructure.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Admin;

[ApiController]
[Route("api/admin/announcements")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminAnnouncementsController(IAnnouncementService announcementService) : ControllerBase
{
    [HttpGet]
    public Task<List<AnnouncementDto>> Get([FromQuery] string? search, CancellationToken cancellationToken) =>
        announcementService.GetAllAsync(search, cancellationToken);

    [HttpPost]
    public Task<AnnouncementDto> Create([FromBody] CreateAnnouncementRequest request, CancellationToken cancellationToken) =>
        announcementService.CreateAsync(User.GetUserId(), request, cancellationToken);

    [HttpPut("{id:int}")]
    public Task<AnnouncementDto> Update(int id, [FromBody] UpdateAnnouncementRequest request, CancellationToken cancellationToken) =>
        announcementService.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await announcementService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
