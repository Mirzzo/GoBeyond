using GoBeyond.EmailConsumer.Services;

namespace GoBeyond.Tests.Email;

public class InFlightSendGateTests
{
    // BG-06's remaining gap: the broker redelivers the SAME message while the first SMTP send is still in
    // flight (e.g. a connection blip triggers RabbitMQ.Client's automatic recovery mid-send). The fake attempt
    // below blocks on a controllable gate, just like a slow SMTP call, so the test deterministically proves the
    // second call waits instead of sending a second time.
    [Fact]
    public async Task RunAsync_SecondCallForSameKey_WaitsForTheFirst_AndDoesNotRunItsOwnAttempt()
    {
        var gate = new InFlightSendGate();
        var firstStarted = new TaskCompletionSource();
        var releaseFirst = new TaskCompletionSource();
        var secondAttemptRan = false;

        var firstTask = gate.RunAsync("key", async () =>
        {
            firstStarted.SetResult();
            await releaseFirst.Task; // stands in for a slow SMTP send
        });

        await firstStarted.Task; // the first call is now registered as in-flight

        var secondTask = gate.RunAsync("key", () =>
        {
            secondAttemptRan = true;
            return Task.CompletedTask;
        });

        // The first attempt is still blocked, so the second call must genuinely be waiting on it, not on its
        // own attempt (which would have completed synchronously).
        Assert.False(secondTask.IsCompleted);

        releaseFirst.SetResult(); // first attempt "sends" successfully

        Assert.True(await firstTask); // the first call actually performed the attempt
        Assert.False(await secondTask); // the second call was told "already sent", did not run its own attempt
        Assert.False(secondAttemptRan);
    }

    [Fact]
    public async Task RunAsync_SecondCallForSameKey_RunsItsOwnAttempt_WhenTheFirstOneFailed()
    {
        var gate = new InFlightSendGate();
        var firstStarted = new TaskCompletionSource();
        var releaseFirst = new TaskCompletionSource();

        var firstTask = gate.RunAsync("key", async () =>
        {
            firstStarted.SetResult();
            await releaseFirst.Task;
            throw new InvalidOperationException("smtp failed");
        });

        await firstStarted.Task;

        var secondAttemptRan = false;
        var secondTask = gate.RunAsync("key", () =>
        {
            secondAttemptRan = true;
            return Task.CompletedTask;
        });

        Assert.False(secondTask.IsCompleted);

        releaseFirst.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => firstTask);
        Assert.True(await secondTask); // took over and ran its own attempt
        Assert.True(secondAttemptRan);
    }

    [Fact]
    public async Task RunAsync_DifferentKeys_DoNotWaitOnEachOther()
    {
        var gate = new InFlightSendGate();
        var release = new TaskCompletionSource();

        var first = gate.RunAsync("a", async () => await release.Task);
        var secondRan = false;
        var second = await gate.RunAsync("b", () =>
        {
            secondRan = true;
            return Task.CompletedTask;
        });

        Assert.True(secondRan);
        Assert.True(second);

        release.SetResult();
        Assert.True(await first);
    }

    [Fact]
    public async Task RunAsync_PropagatesTheException_WhenTheLeaderAttemptFails()
    {
        var gate = new InFlightSendGate();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gate.RunAsync("key", () => throw new InvalidOperationException("smtp failed")));
    }

    [Fact]
    public async Task RunAsync_CanBeCalledAgain_ForTheSameKey_AfterItCompleted()
    {
        var gate = new InFlightSendGate();
        Assert.True(await gate.RunAsync("key", () => Task.CompletedTask));
        Assert.True(await gate.RunAsync("key", () => Task.CompletedTask)); // not stuck "in flight" forever
    }
}
