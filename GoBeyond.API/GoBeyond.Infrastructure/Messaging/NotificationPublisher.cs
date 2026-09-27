using GoBeyond.Core.Entities;
using GoBeyond.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Messaging;

public sealed class NotificationPublisher(GoBeyondDbContext db) : INotificationPublisher
{
    public async Task PublishAsync(string eventType, string recipientEmail, string subject, string body,
        CancellationToken cancellationToken = default)
    {
        var user = db.Users.Local.FirstOrDefault(x => x.Email == recipientEmail)
            ?? await db.Users.FirstOrDefaultAsync(x => x.Email == recipientEmail, cancellationToken);
        db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = eventType, RecipientEmail = recipientEmail, Subject = subject, Body = body,
            User = user
        });
    }
}
