using GoBeyond.API.Extensions;
using GoBeyond.Infrastructure.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoBeyond.Core.Entities;

namespace GoBeyond.API.Controllers;

[Authorize]
[ApiController]
[Route("api/activity")]
public class ActivityController(GoBeyondDbContext dbContext) : ControllerBase
{
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var userId = User.GetUserId();
        var day = now.Date;
        var activity = await dbContext.UserActivities.FirstOrDefaultAsync(
            x => x.UserId == userId && x.Day == day, cancellationToken);
        if (activity is null)
        {
            dbContext.UserActivities.Add(new UserActivity
            {
                UserId = userId, Day = day, LastHeartbeatAt = now
            });
        }
        else
        {
            var elapsed = (now - activity.LastHeartbeatAt).TotalSeconds;
            if (elapsed >= 45)
            {
                activity.ActiveSeconds += (int)Math.Min(elapsed, 90);
                activity.LastHeartbeatAt = now;
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
