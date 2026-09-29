using System.Text.Json;
using GoBeyond.Contracts;
using GoBeyond.Contracts.Messages;
using GoBeyond.EmailConsumer;
using GoBeyond.EmailConsumer.Options;
using GoBeyond.EmailConsumer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GoBeyond.Tests.Email;

/// <summary>
/// Worker.HandleAsync over a real <see cref="InFlightSendGate"/> and <see cref="SentMessageIdStore"/>, with a
/// blocking fake SMTP sender, so the tests cover how Worker wires the gate and the store, not just the gate alone.
/// </summary>
public class WorkerHandleAsyncTests
{
    private static readonly RabbitMqOptions Settings = new()
    {
        Queue = "gobeyond.notifications",
        DeadLetterQueue = "gobeyond.notifications.dead",
        MaxAttempts = 5,
        RetrySeconds = 1
    };

    private static readonly EmailNotificationMessage Message =
        new(1, "ClientRegistered", "klijent@example.org", "Dobrodošli", "Pozdrav,\n\nHvala na registraciji.", CreatedAtTicks: 111);

    private static BasicDeliverEventArgs Delivery(ulong deliveryTag) =>
        new("consumer-tag", deliveryTag, false, string.Empty, Settings.Queue, new BasicProperties(),
            JsonSerializer.SerializeToUtf8Bytes(Message), CancellationToken.None);

    private static Worker NewWorker(IEmailSender sender, SentMessageIdStore store) =>
        new(NullLogger<Worker>.Instance, Options.Create(Settings),
            Options.Create(new SmtpOptions { Host = "smtp.example.com" }), // a real SMTP host, no suppressed domains
            sender, store, new InFlightSendGate());

    // RabbitMQ.Client's automatic recovery can redeliver a message while the first handler is still blocked on
    // the SMTP reply: the redelivery must wait for that send instead of sending its own copy.
    [Fact]
    public async Task HandleAsync_RedeliveryWhileTheFirstSendIsBlocked_SendsOnce_AndAcksBoth()
    {
        var sender = new BlockingEmailSender();
        var worker = NewWorker(sender, new SentMessageIdStore(path: null));
        var channel = new NoOpChannel();

        var first = worker.HandleAsync(channel, Delivery(1), Settings, CancellationToken.None);
        var redelivery = worker.HandleAsync(channel, Delivery(2), Settings, CancellationToken.None);
        Assert.False(first.IsCompleted);
        Assert.False(redelivery.IsCompleted);
        Assert.Equal(1, sender.CallCount);

        sender.Release();
        await Task.WhenAll(first, redelivery).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1, sender.CallCount);
        Assert.Equal([1UL, 2UL], channel.Acked);
    }

    // Redelivery after a process restart: the key is already in the store, so nothing is sent.
    [Fact]
    public async Task HandleAsync_MessageAlreadyRecordedAsSent_IsAckedWithoutSending()
    {
        var sender = new BlockingEmailSender();
        var store = new SentMessageIdStore(path: null);
        store.MarkSent(EmailIdempotencyKey.For(Message));
        var channel = new NoOpChannel();

        await NewWorker(sender, store).HandleAsync(channel, Delivery(1), Settings, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(0, sender.CallCount);
        Assert.Equal([1UL], channel.Acked);
    }

    [Fact]
    public async Task HandleAsync_RecordsTheKeyAsSent_AfterASuccessfulSend()
    {
        var sender = new BlockingEmailSender();
        sender.Release();
        var store = new SentMessageIdStore(path: null);

        await NewWorker(sender, store).HandleAsync(new NoOpChannel(), Delivery(1), Settings, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(store.WasSent(EmailIdempotencyKey.For(Message)));
    }
}
