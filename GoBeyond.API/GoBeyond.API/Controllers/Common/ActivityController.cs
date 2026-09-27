using GoBeyond.API.Extensions;
using GoBeyond.Infrastructure.Services.Activity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Common;

[ApiController]
[Route("api/activity")]
[Authorize]
public sealed class ActivityController(IActivityService activityService) : ControllerBase
{
    /// <summary>Poziva se svakih 60 s dok je aplikacija aktivna; pripisuje najviše 90 s po pozivu.</summary>
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(CancellationToken cancellationToken)
    {
        await activityService.HeartbeatAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }
}
