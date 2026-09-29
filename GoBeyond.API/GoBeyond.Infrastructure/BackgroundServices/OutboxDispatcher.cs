using System.Text.Json;
using GoBeyond.Contracts;
using GoBeyond.Contracts.Messages;
using GoBeyond.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace GoBeyond.Infrastructure.BackgroundServices;

/// <summary>
/// Transactional outbox → RabbitMQ. Emailovi se prvo upisuju u tabelu OutboxMessages (u istoj transakciji
/// kao domenska promjena), a ovaj hosted servis ih objavljuje na queue (gobeyond.notifications) sa
/// potvrdom brokera. Ako objava poruke ne uspije MaxAttempts puta, poruka se označava neuspjelom (FailedAt).
/// Ako broker nije dostupan, poruke čekaju u bazi (ne troše pokušaje).
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = settings.Host,
                    Port = settings.Port,
                    UserName = settings.Username,
                    Password = settings.Password,
                    VirtualHost = settings.VirtualHost,
                    ClientProvidedName = "gobeyond-api-outbox"
                };
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                    stoppingToken);
                await channel.QueueDeclareAsync(settings.Queue, durable: true, exclusive: false, autoDelete: false,
                    cancellationToken: stoppingToken);
                logger.LogInformation("Outbox dispatcher connected to RabbitMQ queue {Queue}.", settings.Queue);

                while (connection.IsOpen && channel.IsOpen && !stoppingToken.IsCancellationRequested)
                {
                    await DispatchBatchAsync(channel, settings, stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, settings.PollIntervalSeconds)), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("RabbitMQ unavailable ({Reason}); pending emails stay in the outbox.", ex.GetType().Name);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, settings.RetrySeconds)), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task DispatchBatchAsync(IChannel channel, RabbitMqOptions settings, CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GoBeyondDbContext>();
        var pending = await db.OutboxMessages
            .Where(x => x.SentAt == null && x.FailedAt == null)
            .OrderBy(x => x.Id)
            .Take(BatchSize)
            .ToListAsync(stoppingToken);

        foreach (var entry in pending)
        {
            entry.Attempts++;
            try
            {
                // CreatedAtTicks lets EmailConsumer build an idempotency key that survives a database reset:
                // entry.Id alone restarts from 1 whenever OutboxMessages is recreated, so without it a
                // different email that reuses an old Id could be mistaken for one already sent (see
                // EmailNotificationMessage's doc comment / EmailConsumer.Services.EmailIdempotencyKey).
                var message = new EmailNotificationMessage(entry.Id, entry.EventType, entry.RecipientEmail, entry.Subject, entry.Body, entry.CreatedAt.Ticks);
                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: settings.Queue,
                    mandatory: true,
                    basicProperties: new BasicProperties
                    {
                        Persistent = true,
                        MessageId = entry.Id.ToString(),
                        ContentType = "application/json"
                    },
                    body: JsonSerializer.SerializeToUtf8Bytes(message),
                    cancellationToken: stoppingToken);
                entry.SentAt = DateTime.UtcNow;
                entry.LastError = null;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                entry.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                if (entry.Attempts >= settings.MaxAttempts)
                {
                    entry.FailedAt = DateTime.UtcNow;
                    logger.LogError("Outbox message {Id} failed {Attempts} times and was marked as failed.", entry.Id, entry.Attempts);
                }
                await db.SaveChangesAsync(stoppingToken);
                throw; // kanal je vjerovatno zatvoren - ponovno povezivanje
            }
            await db.SaveChangesAsync(stoppingToken);
        }
    }
}
