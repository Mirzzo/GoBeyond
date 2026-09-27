using System.Text;
using System.Text.Json;
using GoBeyond.Contracts;
using GoBeyond.Contracts.Messages;
using GoBeyond.EmailConsumer.Services;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GoBeyond.EmailConsumer;

/// <summary>
/// Pomoćni mikroservis: sluša RabbitMQ queue "gobeyond.notifications" i šalje emailove preko SMTP-a.
/// Neuspjela poruka se ponovo objavljuje sa uvećanim brojačem pokušaja (header x-attempt);
/// nakon MaxAttempts (5) ili za neispravan JSON ide u dead-letter queue - nema beskonačnog requeue-a.
/// </summary>
public sealed class Worker(ILogger<Worker> logger, IOptions<RabbitMqOptions> options, IEmailSender emailSender) : BackgroundService
{
    private const string AttemptHeader = "x-attempt";

    /// <summary>Docker healthcheck provjerava da je ovaj fajl svjež (consumer je povezan na broker).</summary>
    private static readonly string HealthFile = Path.Combine(Path.GetTempPath(), "gobeyond-consumer.alive");

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
                    ClientProvidedName = "gobeyond-email-consumer"
                };
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync(settings.Queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync(settings.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, delivery) => HandleAsync(channel, delivery, settings, stoppingToken);
                await channel.BasicConsumeAsync(settings.Queue, autoAck: false, consumer, stoppingToken);
                logger.LogInformation("Listening on queue {Queue} (dead-letter: {DeadLetterQueue}).", settings.Queue, settings.DeadLetterQueue);

                while (connection.IsOpen && channel.IsOpen && !stoppingToken.IsCancellationRequested)
                {
                    await File.WriteAllTextAsync(HealthFile, DateTime.UtcNow.ToString("O"), stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("RabbitMQ connection failed ({Reason}); retrying in {Seconds} s.", ex.GetType().Name, settings.RetrySeconds);
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

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs delivery, RabbitMqOptions settings, CancellationToken stoppingToken)
    {
        var attempt = ReadAttempt(delivery.BasicProperties) + 1;
        EmailNotificationMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<EmailNotificationMessage>(delivery.Body.Span);
        }
        catch (JsonException)
        {
            message = null;
        }

        if (message is null || string.IsNullOrWhiteSpace(message.RecipientEmail))
        {
            logger.LogError("Invalid notification payload moved to dead-letter queue.");
            await PublishAsync(channel, settings.DeadLetterQueue, delivery, attempt, stoppingToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
            return;
        }

        try
        {
            await emailSender.SendAsync(message.RecipientEmail, message.Subject, message.Body, stoppingToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
            logger.LogInformation("Email {MessageId} ({EventType}) sent to {Recipient}.", message.MessageId, message.EventType, message.RecipientEmail);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            if (attempt >= settings.MaxAttempts)
            {
                logger.LogError("Email {MessageId} failed {Attempts} times ({Reason}); moved to dead-letter queue.",
                    message.MessageId, attempt, ex.Message);
                await PublishAsync(channel, settings.DeadLetterQueue, delivery, attempt, stoppingToken);
            }
            else
            {
                logger.LogWarning("Email {MessageId} failed (attempt {Attempt}/{Max}): {Reason}. Retrying in {Seconds} s.",
                    message.MessageId, attempt, settings.MaxAttempts, ex.Message, settings.RetrySeconds);
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, settings.RetrySeconds)), stoppingToken);
                await PublishAsync(channel, settings.Queue, delivery, attempt, stoppingToken);
            }
            await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
        }
    }

    private static int ReadAttempt(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is null || !properties.Headers.TryGetValue(AttemptHeader, out var value) || value is null) return 0;
        return value switch
        {
            int number => number,
            long number => (int)number,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
            _ => 0
        };
    }

    private static async Task PublishAsync(IChannel channel, string queue, BasicDeliverEventArgs delivery, int attempt, CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = delivery.BasicProperties.ContentType,
            MessageId = delivery.BasicProperties.MessageId,
            Headers = new Dictionary<string, object?> { [AttemptHeader] = attempt }
        };
        await channel.BasicPublishAsync(string.Empty, queue, false, properties, delivery.Body, cancellationToken);
    }
}
