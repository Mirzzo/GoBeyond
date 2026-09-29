using GoBeyond.EmailConsumer;

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
}
