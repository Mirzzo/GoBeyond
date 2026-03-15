using GoBeyond.API.Extensions;
using GoBeyond.API.Utilities;
using GoBeyond.Core.DTOs;
using GoBeyond.Infrastructure.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.API.Controllers;

[Authorize]
[ApiController]
[Route("api/notifications")]
public class NotificationsController(GoBeyondDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<NotificationDto>> GetNotifications(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var query = dbContext.Notifications
            .Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            var hasTypeFilter = Enum.TryParse<GoBeyond.Core.Enums.NotificationType>(search, ignoreCase: true, out var parsedType);
            query = query.Where(x =>
                x.Title.ToLower().Contains(normalizedSearch) ||
                x.Body.ToLower().Contains(normalizedSearch) ||
                (hasTypeFilter && x.Type == parsedType));
        }

        var notifications = await query
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return notifications
            .Select(DtoMapper.ToNotificationDto)
            .ToList();
    }

    [HttpPut("{id:int}/read")]
    public async Task<NotificationDto> MarkAsRead(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var notification = await dbContext.Notifications
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Notification not found.");

        notification.IsRead = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        return DtoMapper.ToNotificationDto(notification);
    }
}
