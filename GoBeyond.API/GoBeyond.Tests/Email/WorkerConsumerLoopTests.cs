using GoBeyond.EmailConsumer;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GoBeyond.Tests.Email;

/// <summary>
/// Unit tests for the pure decision function <see cref="Worker.ShouldKeepConsuming"/>, extracted from
/// <c>Worker.ExecuteAsync</c>'s inner health-file loop so it can be tested without a real RabbitMQ
/// connection/channel/consumer. BG-02d: before this fix, the loop condition was
/// <c>connection.IsOpen &amp;&amp; channel.IsOpen &amp;&amp; !stoppingToken.IsCancellationRequested</c> - it never
/// checked whether the broker had cancelled the consumer (e.g. by deleting the queue). A broker-side
/// basic.cancel leaves the connection and channel open, so the consumer looked alive forever while no mail
/// was delivered, and the health file kept being refreshed.
/// </summary>
public class WorkerConsumerLoopTests
{
    [Fact]
    public void ShouldKeepConsuming_ReturnsTrue_WhenConnectionAndChannelOpenAndConsumerNotCancelled()
    {
        Assert.True(Worker.ShouldKeepConsuming(connectionOpen: true, channelOpen: true, consumerCancelled: false, CancellationToken.None));
    }

    // This is the exact BG-02d regression: connection and channel stay open (no exception, no reconnect
    // triggered by the outer catch block) but the broker has cancelled the consumer (queue deleted).
    [Fact]
    public void ShouldKeepConsuming_ReturnsFalse_WhenConsumerCancelled_EvenThoughConnectionAndChannelStillOpen()
    {
        Assert.False(Worker.ShouldKeepConsuming(connectionOpen: true, channelOpen: true, consumerCancelled: true, CancellationToken.None));
    }

    [Fact]
    public void ShouldKeepConsuming_ReturnsFalse_WhenConnectionClosed()
    {
        Assert.False(Worker.ShouldKeepConsuming(connectionOpen: false, channelOpen: true, consumerCancelled: false, CancellationToken.None));
    }

    [Fact]
    public void ShouldKeepConsuming_ReturnsFalse_WhenChannelClosed()
    {
        Assert.False(Worker.ShouldKeepConsuming(connectionOpen: true, channelOpen: false, consumerCancelled: false, CancellationToken.None));
    }

    [Fact]
    public void ShouldKeepConsuming_ReturnsFalse_WhenStoppingTokenCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.False(Worker.ShouldKeepConsuming(connectionOpen: true, channelOpen: true, consumerCancelled: false, cts.Token));
    }

    // The tests above only exercise ShouldKeepConsuming, a pure boolean AND. They still pass even if the actual
    // event wiring (consumer.UnregisteredAsync/ShutdownAsync -> cancelled.TrySetResult()) is deleted from
    // Worker.ExecuteAsync, because nothing calls Worker.WireCancellationSignal at all. The tests below exercise
    // that wiring directly against a REAL AsyncEventingBasicConsumer (with a stub IChannel - NoOpChannel - that
    // is never actually used), calling the same Handle*Async methods RabbitMQ.Client's own dispatch loop calls
    // when the broker cancels the consumer or the channel shuts down.

    [Fact]
    public async Task WireCancellationSignal_CompletesTask_WhenBrokerCancelsConsumer()
    {
        // Simulates BG-02d: the broker sends basic.cancel (e.g. because the queue was deleted). The channel
        // and connection stay open; only the consumer is unregistered.
        var consumer = new AsyncEventingBasicConsumer(new NoOpChannel());
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Worker.WireCancellationSignal(consumer, cancelled, NullLogger.Instance, CancellationToken.None);

        Assert.False(cancelled.Task.IsCompleted);

        await consumer.HandleBasicCancelAsync("consumer-tag", CancellationToken.None);

        Assert.True(cancelled.Task.IsCompleted);
    }

    [Fact]
    public async Task WireCancellationSignal_CompletesTask_WhenChannelShutsDown()
    {
        // Simulates a dropped/force-closed connection (e.g. BG-02d's force-close variant): the channel itself
        // shuts down, which also unregisters the consumer.
        var consumer = new AsyncEventingBasicConsumer(new NoOpChannel());
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Worker.WireCancellationSignal(consumer, cancelled, NullLogger.Instance, CancellationToken.None);

        var reason = new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED - Closed via management plugin");
        await consumer.HandleChannelShutdownAsync(new NoOpChannel(), reason);

        Assert.True(cancelled.Task.IsCompleted);
    }

    [Fact]
    public async Task WireCancellationSignal_DoesNotCompleteTask_WhenConsumerIsRegisteredOk()
    {
        // Control: a normal successful registration (basic.consume-ok) must NOT trigger a reconnect.
        var consumer = new AsyncEventingBasicConsumer(new NoOpChannel());
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Worker.WireCancellationSignal(consumer, cancelled, NullLogger.Instance, CancellationToken.None);

        await consumer.HandleBasicConsumeOkAsync("consumer-tag", CancellationToken.None);

        Assert.False(cancelled.Task.IsCompleted);
    }

    [Fact]
    public async Task ShutdownAsync_LogsReconnecting_WhenInitiatedByThePeer()
    {
        var consumer = new AsyncEventingBasicConsumer(new NoOpChannel());
        var logger = new RecordingLogger();
        Worker.WireCancellationSignal(consumer, new TaskCompletionSource(), logger, CancellationToken.None);

        var reason = new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED - Closed via management plugin");
        await consumer.HandleChannelShutdownAsync(new NoOpChannel(), reason);

        Assert.Contains(logger.Messages, m => m.Contains("reconnecting"));
    }

    [Fact]
    public async Task ShutdownAsync_DoesNotLogReconnecting_WhenTheDisposeIsSelfInitiated()
    {
        // A self-initiated Close/Dispose (Initiator == Application) happens both on a graceful stop and when
        // the Worker tears down a connection it already knows is dead before reconnecting - neither is new
        // information that warrants a "reconnecting" warning.
        var consumer = new AsyncEventingBasicConsumer(new NoOpChannel());
        var logger = new RecordingLogger();
        Worker.WireCancellationSignal(consumer, new TaskCompletionSource(), logger, CancellationToken.None);

        var reason = new ShutdownEventArgs(ShutdownInitiator.Application, 200, "Goodbye");
        await consumer.HandleChannelShutdownAsync(new NoOpChannel(), reason);

        Assert.DoesNotContain(logger.Messages, m => m.Contains("reconnecting"));
    }

    [Fact]
    public async Task ShutdownAsync_DoesNotLogReconnecting_DuringAGracefulStop_EvenIfPeerInitiated()
    {
        using var stopping = new CancellationTokenSource();
        var consumer = new AsyncEventingBasicConsumer(new NoOpChannel());
        var logger = new RecordingLogger();
        Worker.WireCancellationSignal(consumer, new TaskCompletionSource(), logger, stopping.Token);
        stopping.Cancel();

        var reason = new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED - Closed via management plugin");
        await consumer.HandleChannelShutdownAsync(new NoOpChannel(), reason);

        Assert.DoesNotContain(logger.Messages, m => m.Contains("reconnecting"));
    }

    [Fact]
    public async Task UnregisteredAsync_DoesNotLogBrokerCancelled_DuringAGracefulStop()
    {
        using var stopping = new CancellationTokenSource();
        var consumer = new AsyncEventingBasicConsumer(new NoOpChannel());
        var logger = new RecordingLogger();
        Worker.WireCancellationSignal(consumer, new TaskCompletionSource(), logger, stopping.Token);
        stopping.Cancel();

        await consumer.HandleBasicCancelAsync("consumer-tag", CancellationToken.None);

        Assert.Empty(logger.Messages);
    }
}
