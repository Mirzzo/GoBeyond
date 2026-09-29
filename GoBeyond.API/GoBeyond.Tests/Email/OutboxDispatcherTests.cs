using System.Text.Json;
using GoBeyond.Contracts;
using GoBeyond.Contracts.Messages;
using GoBeyond.Core.Entities;
using GoBeyond.EmailConsumer.Services;
using GoBeyond.Infrastructure.BackgroundServices;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Tests.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Email;

/// <summary>
/// OutboxDispatcher.DispatchBatchAsync over a SQLite database and a recording channel: what the API actually puts on
/// the queue for EmailConsumer.
/// </summary>
public sealed class OutboxDispatcherTests : IDisposable
{
    private static readonly RabbitMqOptions Settings = new() { Queue = "gobeyond.notifications", MaxAttempts = 5 };

    private readonly SubscriptionTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    // EmailConsumer's idempotency key includes CreatedAtTicks, because MessageId restarts from 1 after a database
    // reset. Without the outbox creation time on the message, a new email that reuses an old Id and has the same
    // content would be taken for one that was already sent.
    [Fact]
    public async Task DispatchBatchAsync_PublishesTheOutboxCreationTimeAsCreatedAtTicks()
    {
        var createdAt = new DateTime(2026, 9, 30, 8, 15, 42, DateTimeKind.Utc).AddTicks(1234567);
        var entry = new OutboxMessage
        {
            EventType = "ClientRegistered", RecipientEmail = "klijent@example.org", Subject = "Dobrodošli",
            Body = "Pozdrav,\n\nHvala na registraciji.", CreatedAt = createdAt
        };
        await _db.RunAsync(async db =>
        {
            db.OutboxMessages.Add(entry);
            await db.SaveChangesAsync();
        });
        var channel = new NoOpChannel();

        await NewDispatcher().DispatchBatchAsync(channel, Settings, CancellationToken.None);

        var published = Assert.Single(channel.Published);
        Assert.Equal(Settings.Queue, published.RoutingKey);
        var message = JsonSerializer.Deserialize<EmailNotificationMessage>(published.Body)!;
        Assert.Equal(new EmailNotificationMessage(entry.Id, "ClientRegistered", "klijent@example.org", "Dobrodošli",
            "Pozdrav,\n\nHvala na registraciji.", createdAt.Ticks), message);
        Assert.NotEqual(EmailIdempotencyKey.For(message with { CreatedAtTicks = 0 }), EmailIdempotencyKey.For(message));
        var stored = await _db.RunAsync(db => db.OutboxMessages.AsNoTracking().SingleAsync());
        Assert.NotNull(stored.SentAt);
    }

    private OutboxDispatcher NewDispatcher()
    {
        var services = new ServiceCollection().AddScoped<GoBeyondDbContext>(_ => _db.CreateContext()).BuildServiceProvider();
        return new OutboxDispatcher(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(Settings),
            NullLogger<OutboxDispatcher>.Instance);
    }
}
