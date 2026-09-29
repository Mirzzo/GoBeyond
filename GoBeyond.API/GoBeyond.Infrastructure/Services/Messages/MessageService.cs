using GoBeyond.Core.DTOs.Communication;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Messages;

public interface IMessageService
{
    Task<List<MessageThreadDto>> GetThreadsAsync(int userId, UserRole role, string? search, CancellationToken cancellationToken = default);
    Task<List<MessageDto>> GetThreadAsync(int userId, UserRole role, int subscriptionId, CancellationToken cancellationToken = default);
    Task<MessageDto> SendAsync(int userId, UserRole role, int subscriptionId, SendMessageRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Interni sistem poruka mentor ↔ klijent; nit poruka = jedna pretplata.</summary>
public sealed class MessageService(GoBeyondDbContext db, INotificationSender notifications) : IMessageService
{
    private static readonly SubscriptionStatus[] MentorVisibleStatuses =
        [SubscriptionStatus.AwaitingMentor, SubscriptionStatus.Active, SubscriptionStatus.Expired, SubscriptionStatus.Cancelled];

    public async Task<List<MessageThreadDto>> GetThreadsAsync(int userId, UserRole role, string? search,
        CancellationToken cancellationToken = default)
    {
        if (role == UserRole.Admin) return [];
        var isMentor = role == UserRole.Mentor;
        var query = VisibleSubscriptions(userId, role);

        if (search.NormalizeSearch() is { } term)
            query = isMentor
                ? query.Where(x => (x.ClientProfile.User.FirstName + " " + x.ClientProfile.User.LastName).Contains(term))
                : query.Where(x => (x.MentorProfile.User.FirstName + " " + x.MentorProfile.User.LastName).Contains(term));

        var threads = await query.Select(x => new
        {
            x.Id,
            x.Status,
            x.CreatedAt,
            OtherName = isMentor
                ? x.ClientProfile.User.FirstName + " " + x.ClientProfile.User.LastName
                : x.MentorProfile.User.FirstName + " " + x.MentorProfile.User.LastName,
            OtherPhoto = isMentor ? x.ClientProfile.User.ProfileImageUrl : x.MentorProfile.User.ProfileImageUrl,
            OtherDeleted = isMentor ? x.ClientProfile.User.IsDeleted : x.MentorProfile.User.IsDeleted,
            LastContent = x.Messages.OrderByDescending(m => m.SentAt).Select(m => m.Content).FirstOrDefault(),
            LastSentAt = x.Messages.Max(m => (DateTime?)m.SentAt),
            Unread = x.Messages.Count(m => !m.IsRead && m.SenderUserId != userId)
        }).ToListAsync(cancellationToken);

        return threads
            .OrderByDescending(x => x.LastSentAt ?? x.CreatedAt)
            .Select(x => new MessageThreadDto
            {
                SubscriptionId = x.Id,
                OtherPartyName = x.OtherName,
                OtherPartyPhotoUrl = x.OtherPhoto,
                LastMessage = x.LastContent,
                LastMessageAt = x.LastSentAt,
                UnreadCount = x.Unread,
                CanSend = CanSend(x.Status) && !x.OtherDeleted
            })
            .ToList();
    }

    public async Task<List<MessageDto>> GetThreadAsync(int userId, UserRole role, int subscriptionId, CancellationToken cancellationToken = default)
    {
        await EnsureAccessAsync(userId, role, subscriptionId, cancellationToken);

        await db.Messages.Where(x => x.SubscriptionId == subscriptionId && x.SenderUserId != userId && !x.IsRead)
            .ExecuteUpdateAsync(x => x.SetProperty(m => m.IsRead, true), cancellationToken);

        return await db.Messages.AsNoTracking()
            .Where(x => x.SubscriptionId == subscriptionId)
            .OrderBy(x => x.SentAt).ThenBy(x => x.Id)
            .Select(x => new MessageDto
            {
                Id = x.Id,
                Content = x.Content,
                SentAt = x.SentAt,
                IsMine = x.SenderUserId == userId,
                SenderName = x.SenderUser.FirstName + " " + x.SenderUser.LastName
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<MessageDto> SendAsync(int userId, UserRole role, int subscriptionId, SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var subscription = await EnsureAccessAsync(userId, role, subscriptionId, cancellationToken);
        if (!CanSend(subscription.Status))
            throw new ValidationException("Poruke se mogu slati samo dok saradnja čeka mentora ili je aktivna.");

        var sender = role == UserRole.Mentor ? subscription.MentorProfile.User : subscription.ClientProfile.User;
        var recipient = role == UserRole.Mentor ? subscription.ClientProfile.User : subscription.MentorProfile.User;
        if (recipient.IsDeleted)
            throw new ValidationException("Druga strana više nije dostupna na platformi.");

        var message = new Message
        {
            SubscriptionId = subscriptionId,
            SenderUserId = userId,
            Content = request.Content.Trim(),
            SentAt = DateTime.UtcNow
        };
        db.Messages.Add(message);
        await NotifyRecipientAsync(recipient, sender, message.Content, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new MessageDto
        {
            Id = message.Id, Content = message.Content, SentAt = message.SentAt, IsMine = true, SenderName = sender.FullName
        };
    }

    /// <summary>Jedna nepročitana NewMessage obavijest po pošiljaocu (ne zatrpava listu obavijesti).</summary>
    private async Task NotifyRecipientAsync(User recipient, User sender, string content, CancellationToken cancellationToken)
    {
        var title = NewMessageTitle(sender);
        var preview = content.Length > 140 ? content[..140] + "…" : content;
        var existing = await UnreadNewMessageNotifications(recipient.Id, sender).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            existing.Title = title;
            existing.Body = preview;
            existing.CreatedAt = DateTime.UtcNow;
            return;
        }
        notifications.Notify(recipient, NotificationType.NewMessage, title, preview, sendEmail: false);
    }

    /// <summary>Naslov NewMessage obavijesti; ime ide iza dvotačke jer se ne može automatski staviti u padež ("od Harisa ...").</summary>
    private static string NewMessageTitle(User sender) => $"Nova poruka: {sender.FullName}";

    /// <summary>
    /// Nepročitane NewMessage obavijesti korisnika od pošiljaoca, uključujući ranije oblike: naslov "Nova poruka od {ime}"
    /// i naslov "Nova poruka" sa tekstom "{ime}: ..." (demo podaci).
    /// </summary>
    private IQueryable<Notification> UnreadNewMessageNotifications(int userId, User sender)
    {
        var title = NewMessageTitle(sender);
        var legacyTitle = $"Nova poruka od {sender.FullName}";
        var legacyBodyPrefix = $"{sender.FullName}: ";
        return db.Notifications.Where(x => x.UserId == userId && x.Type == NotificationType.NewMessage && !x.IsRead &&
                                           (x.Title == title || x.Title == legacyTitle ||
                                            (x.Title == "Nova poruka" && x.Body.StartsWith(legacyBodyPrefix))));
    }

    private static bool CanSend(SubscriptionStatus status) =>
        status is SubscriptionStatus.AwaitingMentor or SubscriptionStatus.Active;

    /// <summary>Mentoru se nikad ne prikazuje zahtjev koji klijent nije platio (ni kad je u međuvremenu otkazan).</summary>
    private IQueryable<Subscription> VisibleSubscriptions(int userId, UserRole role) => role == UserRole.Mentor
        ? db.Subscriptions.AsNoTracking().Where(x => x.MentorProfile.UserId == userId && MentorVisibleStatuses.Contains(x.Status) && x.PaidAt != null)
        : db.Subscriptions.AsNoTracking().Where(x => x.ClientProfile.UserId == userId && x.Status != SubscriptionStatus.PendingPayment);

    private async Task<Subscription> EnsureAccessAsync(int userId, UserRole role, int subscriptionId, CancellationToken cancellationToken)
    {
        if (role == UserRole.Admin) throw new ForbiddenException("Administrator nema pristup porukama između mentora i klijenta.");
        return await VisibleSubscriptions(userId, role)
                   .Include(x => x.ClientProfile).ThenInclude(x => x.User)
                   .Include(x => x.MentorProfile).ThenInclude(x => x.User)
                   .FirstOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken)
               ?? throw new NotFoundException("Razgovor nije pronađen.");
    }
}
