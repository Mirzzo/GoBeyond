using GoBeyond.EmailConsumer.Services;

namespace GoBeyond.Tests.Email;

public class InFlightSendGateTests
{
    private static readonly Func<Task<bool>> Sends = () => Task.FromResult(true);

    // The broker redelivers the SAME message while the first SMTP send is still in flight (e.g. RabbitMQ.Client's
    // automatic recovery after a connection blip). The fake attempt blocks like a slow SMTP call, so the test
    // deterministically proves the second call waits instead of sending a second time.
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
            return true;
        });

        await firstStarted.Task; // the first call is now registered as in-flight

        var secondTask = gate.RunAsync("key", () =>
        {
            secondAttemptRan = true;
            return Task.FromResult(true);
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
            return Task.FromResult(true);
        });

        Assert.False(secondTask.IsCompleted);

        releaseFirst.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => firstTask);
        Assert.True(await secondTask); // took over and ran its own attempt
        Assert.True(secondAttemptRan);
    }

    // An attempt that finds the key already recorded as sent returns false; for anyone waiting on it that still
    // means "the email is out".
    [Fact]
    public async Task RunAsync_ReturnsFalse_AndDoesNotLetAWaiterSend_WhenTheAttemptFoundItAlreadySent()
    {
        var gate = new InFlightSendGate();
        var releaseFirst = new TaskCompletionSource();

        var firstTask = gate.RunAsync("key", async () =>
        {
            await releaseFirst.Task;
            return false;
        });
        var secondAttemptRan = false;
        var secondTask = gate.RunAsync("key", () =>
        {
            secondAttemptRan = true;
            return Task.FromResult(true);
        });

        releaseFirst.SetResult();

        Assert.False(await firstTask);
        Assert.False(await secondTask);
        Assert.False(secondAttemptRan);
    }

    [Fact]
    public async Task RunAsync_DifferentKeys_DoNotWaitOnEachOther()
    {
        var gate = new InFlightSendGate();
        var release = new TaskCompletionSource();

        var first = gate.RunAsync("a", async () =>
        {
            await release.Task;
            return true;
        });
        var secondRan = false;
        var second = await gate.RunAsync("b", () =>
        {
            secondRan = true;
            return Task.FromResult(true);
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
        Assert.True(await gate.RunAsync("key", Sends));
        Assert.True(await gate.RunAsync("key", Sends)); // not stuck "in flight" forever
    }
}
