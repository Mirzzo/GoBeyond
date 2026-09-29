using GoBeyond.EmailConsumer;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GoBeyond.Tests.Email;

/// <summary>
/// Unit tests for the pure decision function <see cref="Worker.ShouldKeepConsuming"/>, extracted from
/// <c>Worker.ExecuteAsync</c>'s inner health-file loop so it can be tested without a real RabbitMQ
/// connection/channel/consumer. A broker-side basic.cancel (e.g. the queue was deleted) leaves the connection
/// and channel open, so the condition must also check whether the consumer was cancelled.
/// </summary>
public class WorkerConsumerLoopTests
{
    [Fact]
    public void ShouldKeepConsuming_ReturnsTrue_WhenConnectionAndChannelOpenAndConsumerNotCancelled()
    {
        Assert.True(Worker.ShouldKeepConsuming(connectionOpen: true, channelOpen: true, consumerCancelled: false, CancellationToken.None));
    }

    // Connection and channel stay open (no exception, no reconnect from the outer catch block) but the broker
    // has cancelled the consumer (queue deleted).
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

    // The tests below exercise the event wiring (Worker.WireCancellationSignal) against a real
    // AsyncEventingBasicConsumer, calling the same Handle*Async methods RabbitMQ.Client's dispatch loop calls
    // when the broker cancels the consumer or the channel shuts down.

    [Fact]
    public async Task WireCancellationSignal_CompletesTask_WhenBrokerCancelsConsumer()
    {
        // The broker sends basic.cancel (e.g. because the queue was deleted). The channel and connection stay
        // open; only the consumer is unregistered.
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
        // A dropped/force-closed connection: the channel itself shuts down, which also unregisters the consumer.
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
