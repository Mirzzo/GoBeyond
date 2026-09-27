using GoBeyond.Core.DTOs.Communication;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Notifications;

public interface INotificationService
{
    Task<List<NotificationDto>> GetMineAsync(int userId, NotificationSearchObject search, CancellationToken cancellationToken = default);
    Task MarkReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(int userId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);
}

public sealed class NotificationService(GoBeyondDbContext db) : INotificationService
{
    public async Task<List<NotificationDto>> GetMineAsync(int userId, NotificationSearchObject search,
        CancellationToken cancellationToken = default)
    {
        var query = db.Notifications.AsNoTracking().Where(x => x.UserId == userId);
        if (search.UnreadOnly == true) query = query.Where(x => !x.IsRead);
        if (search.Search.NormalizeSearch() is { } term)
            query = query.Where(x => x.Title.Contains(term) || x.Body.Contains(term));

        return await query
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(200)
            .Select(x => new NotificationDto
            {
                Id = x.Id, Title = x.Title, Body = x.Body, Type = x.Type, IsRead = x.IsRead, CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task MarkReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, cancellationToken)
                           ?? throw new NotFoundException("Obavijest nije pronađena.");
        notification.IsRead = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task MarkAllReadAsync(int userId, CancellationToken cancellationToken = default) =>
        db.Notifications.Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(x => x.SetProperty(n => n.IsRead, true), cancellationToken);

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default) =>
        db.Notifications.CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);
}
