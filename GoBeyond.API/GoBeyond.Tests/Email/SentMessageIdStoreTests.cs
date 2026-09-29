using GoBeyond.EmailConsumer.Services;

namespace GoBeyond.Tests.Email;

public class SentMessageIdStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"gobeyond-sent-ids-test-{Guid.NewGuid():N}.txt");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void WasSent_ReturnsFalse_ForUnknownId()
    {
        var store = new SentMessageIdStore(_path);
        Assert.False(store.WasSent(109));
    }

    [Fact]
    public void WasSent_ReturnsTrue_AfterMarkSent()
    {
        var store = new SentMessageIdStore(_path);
        store.MarkSent(109);
        Assert.True(store.WasSent(109));
    }

    // BG-06: the consumer sent to SMTP, then was killed before BasicAck. The broker redelivered the message
    // and it was sent a second time because nothing remembered that id=109 had already gone out. A restarted
    // consumer must construct a *new* SentMessageIdStore instance (same file) and still recognize the id.
    [Fact]
    public void WasSent_SurvivesAcrossNewStoreInstance_SimulatingConsumerRestartAfterCrash()
    {
        var firstRun = new SentMessageIdStore(_path);
        firstRun.MarkSent(109); // consumer sent the email, wrote the id, then was killed before BasicAck

        var afterRestart = new SentMessageIdStore(_path); // broker redelivers id=109 to the restarted consumer

        Assert.True(afterRestart.WasSent(109));
    }

    [Fact]
    public void MarkSent_IsIdempotent_DoesNotDuplicateEntryOrThrow()
    {
        var store = new SentMessageIdStore(_path);
        store.MarkSent(9721);
        store.MarkSent(9721);

        var reloaded = new SentMessageIdStore(_path);
        Assert.True(reloaded.WasSent(9721));
        Assert.Single(File.ReadAllLines(_path), l => l == "9721");
    }

    [Fact]
    public void IsEnabled_IsFalse_ForEmptyPath()
    {
        var store = new SentMessageIdStore(string.Empty);
        Assert.False(store.IsEnabled);
    }

    [Fact]
    public void WasSent_AlwaysFalse_AndMarkSentIsNoOp_WhenDisabled()
    {
        var store = new SentMessageIdStore(path: null);
        store.MarkSent(1);
        Assert.False(store.WasSent(1));
    }

    [Fact]
    public void Store_IsBoundedInSize_OldestIdsAreEvictedOnceTheFileIsCompacted()
    {
        var store = new SentMessageIdStore(_path);
        var total = SentMessageIdStore.MaxEntries + 500; // enough to trigger at least one compaction
        for (var id = 1; id <= total; id++)
            store.MarkSent(id);

        // The oldest ids fall off; the file never grows without bound.
        Assert.False(store.WasSent(1));
        Assert.True(store.WasSent(total));
        Assert.True(File.ReadAllLines(_path).Length <= SentMessageIdStore.MaxEntries + 200);

        // A fresh instance loaded from the compacted file still recognizes the most recent ids.
        var reloaded = new SentMessageIdStore(_path);
        Assert.True(reloaded.WasSent(total));
        Assert.False(reloaded.WasSent(1));
    }
}
