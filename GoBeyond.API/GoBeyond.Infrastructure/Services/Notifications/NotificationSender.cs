using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Database;

namespace GoBeyond.Infrastructure.Services.Notifications;

/// <summary>
/// Kreira in-app obavijest i (opciono) email preko transactional outbox-a.
/// Ništa ne snima: pozivalac radi SaveChanges zajedno sa domenskom promjenom, pa se
/// obavijest, email i promjena upisuju u istoj transakciji. OutboxDispatcher zatim
/// objavljuje email na RabbitMQ, a GoBeyond.EmailConsumer ga šalje preko SMTP-a.
/// Primalac mora biti već snimljen korisnik (postavlja se samo UserId, ne navigacija).
/// </summary>
public interface INotificationSender
{
    Notification Notify(User recipient, NotificationType type, string title, string body, bool sendEmail);

    void QueueEmail(User recipient, string eventType, string subject, string body);
}

public sealed class NotificationSender(GoBeyondDbContext db) : INotificationSender
{
    public Notification Notify(User recipient, NotificationType type, string title, string body, bool sendEmail)
    {
        var notification = new Notification
        {
            UserId = recipient.Id,
            Type = type,
            Title = title,
            Body = body,
            CreatedAt = DateTime.UtcNow
        };
        db.Notifications.Add(notification);

        if (sendEmail)
            QueueEmail(recipient, type.ToString(), title, body);

        return notification;
    }

    public void QueueEmail(User recipient, string eventType, string subject, string body)
    {
        // Obrisanim korisnicima se ne šalju emailovi.
        if (recipient.IsDeleted || string.IsNullOrWhiteSpace(recipient.Email)) return;

        db.OutboxMessages.Add(new OutboxMessage
        {
            UserId = recipient.Id,
            EventType = eventType,
            RecipientEmail = recipient.Email,
            Subject = $"GoBeyond: {subject}",
            Body = $"Pozdrav {recipient.FirstName},\n\n{body}\n\nVaš GoBeyond tim",
            CreatedAt = DateTime.UtcNow
        });
    }
}
