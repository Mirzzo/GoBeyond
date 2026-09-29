using System.Text;
using System.Text.Json;
using GoBeyond.Contracts;
using GoBeyond.Contracts.Messages;
using GoBeyond.EmailConsumer.Options;
using GoBeyond.EmailConsumer.Services;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GoBeyond.EmailConsumer;

/// <summary>
/// Pomoćni mikroservis: sluša RabbitMQ queue "gobeyond.notifications" i šalje emailove preko SMTP-a.
/// Neuspjela poruka se ponovo objavljuje sa uvećanim brojačem pokušaja (header x-attempt);
/// nakon MaxAttempts (5), za neispravan JSON ili za poruku bez obaveznih polja (<see cref="NotificationMessageValidator"/>)
/// ide u dead-letter queue - nema beskonačnog requeue-a i nema slanja praznog emaila.
/// Poruke za primaoce na zaštićenim domenama (<see cref="SmtpOptions.SuppressedRecipientDomains"/>) se
/// ne šalju kad host nije Mailpit - vidi <see cref="RecipientSuppression"/>. Poruke koje su već jednom
/// uspješno poslane (redelivery nakon pada procesa) se prepoznaju preko <see cref="SentMessageIdStore"/> i
/// samo ack-uju, bez ponovnog slanja; redelivery DOK je prvi pokušaj još u toku pokriva <see cref="InFlightSendGate"/>.
/// </summary>
public sealed class Worker(
    ILogger<Worker> logger,
    IOptions<RabbitMqOptions> options,
    IOptions<SmtpOptions> smtpOptions,
    IEmailSender emailSender,
    SentMessageIdStore sentMessageIds,
    InFlightSendGate inFlightSends) : BackgroundService
{
    private const string AttemptHeader = "x-attempt";

    /// <summary>Docker healthcheck provjerava da je ovaj fajl svjež (consumer je stvarno registrovan kod brokera).</summary>
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

                // Broker koji obriše queue (ili na drugi način otkaže ovog consumer-a) šalje basic.cancel: konekcija
                // i kanal ostaju IsOpen (nema greške), samo consumer prestaje da prima isporuke. To se
                // prati preko UnregisteredAsync/ShutdownAsync (ne pollingom IsRunning - odmah nakon BasicConsumeAsync
                // bi to moglo kratko biti false dok potvrda registracije ne stigne, pa bi polling lažno okinuo
                // reconnect u petlji); kad se okine bilo koji od njih, unutrašnja petlja ispod se prekida, `await
                // using` zatvara staru konekciju/kanal, a vanjska petlja se odmah ponovo poveže i re-deklariše queue.
                var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, delivery) => HandleAsync(channel, delivery, settings, stoppingToken);
                WireCancellationSignal(consumer, cancelled, logger, stoppingToken);
                await channel.BasicConsumeAsync(settings.Queue, autoAck: false, consumer, stoppingToken);
                logger.LogInformation("Listening on queue {Queue} (dead-letter: {DeadLetterQueue}).", settings.Queue, settings.DeadLetterQueue);

                while (ShouldKeepConsuming(connection.IsOpen, channel.IsOpen, cancelled.Task.IsCompleted, stoppingToken))
                {
                    // Fajl se piše SAMO dok je consumer stvarno registrovan, da docker healthcheck ne prijavi
                    // "healthy" dok je broker otkazao consumer-a i pošta stoji neisporučena.
                    await File.WriteAllTextAsync(HealthFile, DateTime.UtcNow.ToString("O"), stoppingToken);
                    await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(15), stoppingToken), cancelled.Task);
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

    /// <summary>
    /// True dok treba nastaviti konzumirati sa trenutnom konekcijom/kanalom: oboje moraju biti otvoreni, consumer
    /// ne smije biti otkazan od strane brokera (<paramref name="consumerCancelled"/>, vidi komentar u
    /// <see cref="ExecuteAsync"/>) i servis ne smije biti u gašenju. Izdvojeno kao čista funkcija radi testiranja
    /// bez pravog RabbitMQ kanala.
    /// </summary>
    public static bool ShouldKeepConsuming(bool connectionOpen, bool channelOpen, bool consumerCancelled, CancellationToken stoppingToken) =>
        connectionOpen && channelOpen && !consumerCancelled && !stoppingToken.IsCancellationRequested;

    /// <summary>
    /// Kači se na <paramref name="consumer"/>-ove UnregisteredAsync/ShutdownAsync evente i signalizira
    /// <paramref name="cancelled"/> prvi put kad se bilo koji od njih okine, tako da unutrašnja petlja u
    /// <see cref="ExecuteAsync"/> stane i vanjska petlja se ponovo poveže. Izdvojeno iz
    /// <see cref="ExecuteAsync"/> u zaseban metod da bi samo kačenje bilo pokriveno testom bez prave RabbitMQ
    /// konekcije - vidi WorkerConsumerLoopTests, koji ovaj metod poziva nad pravim
    /// <see cref="AsyncEventingBasicConsumer"/>-om i onda direktno zove
    /// HandleBasicCancelAsync/HandleChannelShutdownAsync - iste metode koje RabbitMQ.Client-ova dispatch petlja
    /// zove kad broker otkaže consumer-a ili kanal padne.
    ///
    /// UnregisteredAsync se okida i za pravi broker-side basic.cancel (npr. obrisan queue - kanal/konekcija
    /// ostaju otvoreni, <c>consumer.ShutdownReason</c> je tad još uvijek null) i kao posljedica pada
    /// kanala/konekcije (tad je ShutdownReason već postavljen, a ShutdownAsync se okine odmah zatim sa istim
    /// razlogom). Logujemo "otkazano od brokera" poruku samo kad je ShutdownReason još null.
    ///
    /// ShutdownAsync se okida i kad MI sami zatvorimo konekciju/kanal (<c>args.Initiator == Application</c>) -
    /// bilo pri gašenju servisa (<paramref name="stoppingToken"/> otkazan) bilo pri raspremanju stare
    /// konekcije/kanala nakon što je vanjska petlja već odlučila da se ponovo poveže. To NIJE nova informacija
    /// koja traži reconnect, pa se "reconnecting" loguje samo kad je razlog stvarno došao od brokera/mreže.
    /// </summary>
    public static void WireCancellationSignal(
        AsyncEventingBasicConsumer consumer, TaskCompletionSource cancelled, ILogger logger, CancellationToken stoppingToken)
    {
        consumer.UnregisteredAsync += (_, _) =>
        {
            if (consumer.ShutdownReason is null && !stoppingToken.IsCancellationRequested)
                logger.LogWarning("Consumer was cancelled by the broker (e.g. the queue was deleted); reconnecting.");
            cancelled.TrySetResult();
            return Task.CompletedTask;
        };
        consumer.ShutdownAsync += (_, args) =>
        {
            if (args.Initiator != ShutdownInitiator.Application && !stoppingToken.IsCancellationRequested)
                logger.LogWarning("Consumer channel shut down ({ReplyText}); reconnecting.", args.ReplyText);
            cancelled.TrySetResult();
            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// Obrađuje jednu isporuku: validacija, supresija, slanje kroz <see cref="InFlightSendGate"/> (sa provjerom
    /// <see cref="SentMessageIdStore"/>) i ack/retry/dead-letter. Public radi testova (WorkerHandleAsyncTests).
    /// </summary>
    public async Task HandleAsync(IChannel channel, BasicDeliverEventArgs delivery, RabbitMqOptions settings, CancellationToken stoppingToken)
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

        if (!NotificationMessageValidator.IsValid(message))
        {
            logger.LogError("Invalid notification payload moved to dead-letter queue.");
            await PublishAsync(channel, settings.DeadLetterQueue, delivery, attempt, stoppingToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
            return;
        }

        var smtp = smtpOptions.Value;
        if (RecipientSuppression.ShouldSuppress(smtp.Host, message.RecipientEmail, smtp.SuppressedRecipientDomains))
        {
            logger.LogInformation(
                "Email {MessageId} ({EventType}) suppressed: recipient domain '{Domain}' is on Smtp:SuppressedRecipientDomains (host {Host} is not Mailpit).",
                message.MessageId, message.EventType, RecipientSuppression.ExtractDomain(message.RecipientEmail), smtp.Host);
            await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
            return;
        }

        // MessageId sam NIJE dovoljan ključ (restartuje se od 1 kad se baza resetuje) - vidi EmailIdempotencyKey.
        var idempotencyKey = EmailIdempotencyKey.For(message);
        if (sentMessageIds.WasSent(idempotencyKey))
        {
            // Redelivery nakon pada procesa između uspješnog SMTP slanja i BasicAck-a (vidi SentMessageIdStore) -
            // email je stvarno već poslan, samo se potvrđuje bez ponovnog slanja.
            logger.LogInformation("Email {MessageId} ({EventType}) already sent earlier (redelivered by the broker); acking without resending.",
                message.MessageId, message.EventType);
            await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
            return;
        }

        bool sentByThisCall;
        try
        {
            // Redelivery ISTOG ključa dok je ovaj pokušaj u toku (npr. RabbitMQ.Client automatski oporavi
            // konekciju dok je SMTP send i dalje u toku) čeka ovaj pokušaj umjesto da šalje ponovo - vidi
            // InFlightSendGate.
            sentByThisCall = await inFlightSends.RunAsync(idempotencyKey,
                () => emailSender.SendAsync(message.RecipientEmail, message.Subject, message.Body, stoppingToken));
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
            return;
        }

        // Loguje se PRIJE ack-a: ako ack propadne (npr. kanal se u međuvremenu zatvorio), trag o uspješnom
        // slanju ostaje u logu umjesto da nestane zajedno sa izuzetkom. MarkSent sam hvata svoje IO greške i
        // nikad ne baca, pa se ack ispod dešava bez obzira na to da li je upis u store uspio.
        if (sentByThisCall)
        {
            sentMessageIds.MarkSent(idempotencyKey);
            logger.LogInformation("Email {MessageId} ({EventType}) sent to {Recipient}.", message.MessageId, message.EventType, message.RecipientEmail);
        }
        else
        {
            logger.LogInformation("Email {MessageId} ({EventType}) already sent by a concurrent in-flight attempt for the same message; acking without resending.",
                message.MessageId, message.EventType);
        }
        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
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
