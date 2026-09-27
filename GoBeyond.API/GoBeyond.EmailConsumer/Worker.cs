using System.Text.Json;
using GoBeyond.Contracts;
using GoBeyond.Contracts.Messages;
using GoBeyond.EmailConsumer.Services;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GoBeyond.EmailConsumer;

public sealed class Worker(ILogger<Worker> logger, IOptions<RabbitMqOptions> options,
    IEmailSender emailSender) : BackgroundService
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
                    ClientProvidedName = "gobeyond-email-consumer", AutomaticRecoveryEnabled = true
                };
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync(settings.Queue, durable: true, exclusive: false,
                    autoDelete: false, cancellationToken: stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        var message = JsonSerializer.Deserialize<EmailNotificationMessage>(delivery.Body.Span)
                            ?? throw new JsonException("Empty notification message.");
                        await emailSender.SendAsync(message.RecipientEmail, message.Subject, message.Body, stoppingToken);
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                        logger.LogInformation("Processed notification {MessageId} ({EventType}).", message.MessageId, message.EventType);
                    }
                    catch (JsonException ex)
                    {
                        logger.LogError(ex, "Invalid notification payload rejected.");
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, false, stoppingToken);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogWarning("Email delivery failed ({Reason}); message will be retried.", ex.GetType().Name);
                        await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, settings.RetrySeconds)), stoppingToken);
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, true, stoppingToken);
                    }
                };
                await channel.BasicConsumeAsync(settings.Queue, autoAck: false, consumer, stoppingToken);
                logger.LogInformation("Listening on notification queue {Queue}.", settings.Queue);
                while (connection.IsOpen && channel.IsOpen && !stoppingToken.IsCancellationRequested)
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Broker connection failed ({Reason}); retrying.", ex.GetType().Name);
                try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, settings.RetrySeconds)), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
