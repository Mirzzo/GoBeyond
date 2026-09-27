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

namespace GoBeyond.Infrastructure.Messaging;

public sealed class OutboxDispatcher(IServiceScopeFactory scopes, IOptions<RabbitMqOptions> options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = settings.Host, Port = settings.Port, UserName = settings.Username,
                    Password = settings.Password, VirtualHost = settings.VirtualHost,
                    ClientProvidedName = "gobeyond-api-outbox", AutomaticRecoveryEnabled = true
                };
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                    stoppingToken);
                await channel.QueueDeclareAsync(settings.Queue, durable: true, exclusive: false,
                    autoDelete: false, cancellationToken: stoppingToken);
                while (connection.IsOpen && channel.IsOpen && !stoppingToken.IsCancellationRequested)
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<GoBeyondDbContext>();
                    var messages = await db.OutboxMessages.Where(x => x.SentAt == null)
                        .OrderBy(x => x.Id).Take(50).ToListAsync(stoppingToken);
                    foreach (var entry in messages)
                    {
                        entry.Attempts++;
                        try
                        {
                            var payload = new EmailNotificationMessage(entry.Id, entry.EventType,
                                entry.RecipientEmail, entry.Subject, entry.Body);
                            await channel.BasicPublishAsync(exchange: string.Empty, routingKey: settings.Queue,
                                mandatory: true, basicProperties: new BasicProperties
                                {
                                    Persistent = true, MessageId = entry.Id.ToString(), ContentType = "application/json"
                                }, body: JsonSerializer.SerializeToUtf8Bytes(payload), cancellationToken: stoppingToken);
                            entry.SentAt = DateTime.UtcNow;
                            entry.LastError = null;
                        }
                        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                        {
                            entry.LastError = ex.GetType().Name;
                            await db.SaveChangesAsync(stoppingToken);
                            throw;
                        }
                        await db.SaveChangesAsync(stoppingToken);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Notification broker unavailable ({Reason}); pending messages remain in the database.", ex.GetType().Name);
                try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, settings.RetrySeconds)), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
