using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Common;
using GoBeyond.Core.DTOs.Communication;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Common;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    public Task<List<NotificationDto>> Get([FromQuery] NotificationSearchObject searchObject, CancellationToken cancellationToken) =>
        notificationService.GetMineAsync(User.GetUserId(), searchObject, cancellationToken);

    [HttpGet("unread-count")]
    public async Task<CountResponse> UnreadCount(CancellationToken cancellationToken) =>
        new(await notificationService.GetUnreadCountAsync(User.GetUserId(), cancellationToken));

    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        await notificationService.MarkReadAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await notificationService.MarkAllReadAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }
}
