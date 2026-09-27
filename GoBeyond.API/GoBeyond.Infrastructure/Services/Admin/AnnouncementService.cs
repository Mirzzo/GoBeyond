using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Admin;

public interface IAnnouncementService
{
    Task<List<AnnouncementDto>> GetAllAsync(string? search, CancellationToken cancellationToken = default);
    Task<AnnouncementDto> CreateAsync(int adminUserId, CreateAnnouncementRequest request, CancellationToken cancellationToken = default);
    Task<AnnouncementDto> UpdateAsync(int id, UpdateAnnouncementRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>Sistemske obavijesti: fan-out in-app obavijesti + email (outbox → RabbitMQ) svim ciljanim korisnicima.</summary>
public sealed class AnnouncementService(GoBeyondDbContext db, INotificationSender notifications) : IAnnouncementService
{
    public async Task<List<AnnouncementDto>> GetAllAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = db.Announcements.AsNoTracking();
        if (search.NormalizeSearch() is { } term)
            query = query.Where(x => x.Title.Contains(term) || x.Content.Contains(term));
        return await query.OrderByDescending(x => x.CreatedAt).Select(Projection).ToListAsync(cancellationToken);
    }

    public async Task<AnnouncementDto> CreateAsync(int adminUserId, CreateAnnouncementRequest request, CancellationToken cancellationToken = default)
    {
        var announcement = new Announcement
        {
            CreatedByUserId = adminUserId,
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            TargetRole = request.TargetRole,
            CreatedAt = DateTime.UtcNow
        };
        db.Announcements.Add(announcement);

        // Primaoci: aktivni, neobrisani korisnici ciljane uloge koji se mogu prijaviti (mentori moraju biti odobreni).
        var recipients = await db.Users
            .Where(x => x.IsActive && !x.IsDeleted &&
                        (request.TargetRole == null || x.Role == request.TargetRole) &&
                        (x.Role != UserRole.Mentor || x.MentorProfile!.Status == MentorApprovalStatus.Approved))
            .ToListAsync(cancellationToken);

        foreach (var recipient in recipients)
        {
            var notification = notifications.Notify(recipient, NotificationType.Announcement,
                announcement.Title, announcement.Content, sendEmail: true);
            notification.Announcement = announcement;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(announcement.Id, cancellationToken);
    }

    public async Task<AnnouncementDto> UpdateAsync(int id, UpdateAnnouncementRequest request, CancellationToken cancellationToken = default)
    {
        var announcement = await db.Announcements.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                           ?? throw new NotFoundException("Sistemska obavijest nije pronađena.");
        announcement.Title = request.Title.Trim();
        announcement.Content = request.Content.Trim();
        announcement.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Briše obavijest i njene nepročitane notifikacije; pročitane ostaju (bez veze na obavijest).</summary>
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var announcement = await db.Announcements.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                           ?? throw new NotFoundException("Sistemska obavijest nije pronađena.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Notifications.Where(x => x.AnnouncementId == id && !x.IsRead).ExecuteDeleteAsync(cancellationToken);
        await db.Notifications.Where(x => x.AnnouncementId == id)
            .ExecuteUpdateAsync(x => x.SetProperty(n => n.AnnouncementId, (int?)null), cancellationToken);
        db.Announcements.Remove(announcement);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private Task<AnnouncementDto> GetAsync(int id, CancellationToken cancellationToken) =>
        db.Announcements.AsNoTracking().Where(x => x.Id == id).Select(Projection).FirstAsync(cancellationToken);

    private static readonly System.Linq.Expressions.Expression<Func<Announcement, AnnouncementDto>> Projection = x => new AnnouncementDto
    {
        Id = x.Id,
        Title = x.Title,
        Content = x.Content,
        TargetRole = x.TargetRole,
        CreatedAt = x.CreatedAt,
        CreatedByName = x.CreatedByUser.FirstName + " " + x.CreatedByUser.LastName,
        RecipientCount = x.Notifications.Count
    };
}
