using GoBeyond.Contracts.Messages;
using GoBeyond.EmailConsumer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoBeyond.Tests.Email;

public class SentMessageIdStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"gobeyond-sent-ids-test-{Guid.NewGuid():N}.txt");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void WasSent_ReturnsFalse_ForUnknownKey()
    {
        var store = new SentMessageIdStore(_path);
        Assert.False(store.WasSent("109:100:abc"));
    }

    [Fact]
    public void WasSent_ReturnsTrue_AfterMarkSent()
    {
        var store = new SentMessageIdStore(_path);
        store.MarkSent("109:100:abc");
        Assert.True(store.WasSent("109:100:abc"));
    }

    // BG-06: the consumer sent to SMTP, then was killed before BasicAck. The broker redelivered the message
    // and it was sent a second time because nothing remembered that this key had already gone out. A
    // restarted consumer must construct a *new* SentMessageIdStore instance (same file) and still recognize it.
    [Fact]
    public void WasSent_SurvivesAcrossNewStoreInstance_SimulatingConsumerRestartAfterCrash()
    {
        var firstRun = new SentMessageIdStore(_path);
        firstRun.MarkSent("109:100:abc"); // consumer sent the email, wrote the key, then was killed before BasicAck

        var afterRestart = new SentMessageIdStore(_path); // broker redelivers the same message to the restarted consumer

        Assert.True(afterRestart.WasSent("109:100:abc"));
    }

    // Review major defect: keying only on the outbox MessageId (109) meant that after a database reset (a
    // fresh dev/test DB, a second install, ...) a COMPLETELY DIFFERENT email that happened to reuse Id 109
    // would be silently acked and never sent. EmailIdempotencyKey.For includes CreatedAtTicks and a content
    // hash, so a genuinely different message - even with the same MessageId - produces a different key here
    // and is correctly treated as NOT sent.
    [Fact]
    public void WasSent_DoesNotTreatAReusedMessageIdWithDifferentContentAsAlreadySent_AfterSimulatedDbReset()
    {
        var store = new SentMessageIdStore(_path);
        var before = new EmailNotificationMessage(109, "ClientRegistered", "rev.e1.first@example.org", "dup A", "body A", CreatedAtTicks: 100);
        store.MarkSent(EmailIdempotencyKey.For(before));

        // Database was reset; OutboxMessages restarted from 1, so a brand new, unrelated email reused Id 109 -
        // but its CreatedAt (and content) are different.
        var after = new EmailNotificationMessage(109, "MentorApproved", "rev.e1.other.user@example.org", "different email same id", "different body", CreatedAtTicks: 999);

        Assert.False(store.WasSent(EmailIdempotencyKey.For(after)));
    }

    [Fact]
    public void MarkSent_IsIdempotent_DoesNotDuplicateEntryOrThrow()
    {
        var store = new SentMessageIdStore(_path);
        store.MarkSent("9721:100:abc");
        store.MarkSent("9721:100:abc");

        var reloaded = new SentMessageIdStore(_path);
        Assert.True(reloaded.WasSent("9721:100:abc"));
        Assert.Single(File.ReadAllLines(_path), l => l == "9721:100:abc");
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
        store.MarkSent("1:1:x");
        Assert.False(store.WasSent("1:1:x"));
    }

    [Fact]
    public void Store_IsBoundedInSize_OldestKeysAreEvictedOnceTheFileIsCompacted()
    {
        var store = new SentMessageIdStore(_path);
        var total = SentMessageIdStore.MaxEntries + 500; // enough to trigger at least one compaction
        for (var id = 1; id <= total; id++)
            store.MarkSent($"{id}:1:x");

        // The oldest keys fall off; the file never grows without bound.
        Assert.False(store.WasSent("1:1:x"));
        Assert.True(store.WasSent($"{total}:1:x"));
        Assert.True(File.ReadAllLines(_path).Length <= SentMessageIdStore.MaxEntries + 200);

        // A fresh instance loaded from the compacted file still recognizes the most recent keys.
        var reloaded = new SentMessageIdStore(_path);
        Assert.True(reloaded.WasSent($"{total}:1:x"));
        Assert.False(reloaded.WasSent("1:1:x"));
    }

    // Review defect: the OLD store format (before EmailIdempotencyKey existed) wrote bare
    // EmailNotificationMessage.MessageId integers, one per line ("109"). Loading that file must not throw, and
    // since a legacy bare-integer line never contains ':' it can never collide with a new composite key -
    // it just sits inert until compaction drops it.
    [Fact]
    public void Load_HandlesOldPlainIntegerFormat_WithoutThrowing_AndNeverMatchesANewCompositeKey()
    {
        File.WriteAllLines(_path, ["109", "9721"]);

        var store = new SentMessageIdStore(_path);

        Assert.False(store.WasSent("109:100:abc")); // new-format key for the same old Id does NOT match
        store.MarkSent("200:1:y");
        Assert.True(store.WasSent("200:1:y"));
    }

    // Review minor defect: a store bookkeeping failure (locked/unreadable file - e.g. a OneDrive-synced repo
    // folder) must not stop the consumer from starting at all. Pointing the path at a DIRECTORY makes
    // File.Exists false (Load returns immediately) as one common failure mode; the constructor must not throw
    // regardless.
    [Fact]
    public void Constructor_DoesNotThrow_WhenPathIsUnreadable()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"gobeyond-sent-ids-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        try
        {
            var store = new SentMessageIdStore(directoryPath, NullLogger.Instance);
            Assert.True(store.IsEnabled);
            Assert.False(store.WasSent("1:1:x")); // started with an empty in-memory set, did not throw
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    // Review minor defect: MarkSent must never propagate an IO failure - the caller (Worker.HandleAsync) calls
    // it AFTER a successful SMTP send and must still be able to ack. Pointing the path at a directory makes
    // the AppendAllText write fail every time.
    [Fact]
    public void MarkSent_DoesNotThrow_WhenTheFileCannotBeWritten()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"gobeyond-sent-ids-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        try
        {
            var store = new SentMessageIdStore(directoryPath, NullLogger.Instance);
            var exception = Record.Exception(() => store.MarkSent("1:1:x"));
            Assert.Null(exception);
            // The in-memory record is still kept for this process, even though the durable write failed.
            Assert.True(store.WasSent("1:1:x"));
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Theory]
    [InlineData("gobeyond-consumer-sent-ids.txt")]
    [InlineData("sub/ids.txt")]
    public void ResolvePath_ResolvesRelativePath_UnderOsTempFolder_NeverUnderCurrentDirectory(string relative)
    {
        // Review defect: a relative path (the appsettings.Shared.json default) must not resolve against the
        // process' current working directory - `dotnet run` from the repo root would otherwise write the file
        // straight into the git-tracked, OneDrive-synced repository.
        var resolved = SentMessageIdStore.ResolvePath(relative);

        Assert.True(Path.IsPathRooted(resolved));
        Assert.StartsWith(Path.GetTempPath(), resolved);
    }

    [Fact]
    public void ResolvePath_LeavesAnAbsolutePath_Unchanged()
    {
        // Docker's absolute override (Smtp__SentMessageIdsFilePath=/data/... on the named volume) must be used
        // exactly as configured.
        var absolute = OperatingSystem.IsWindows() ? @"C:\data\gobeyond-consumer-sent-ids.txt" : "/data/gobeyond-consumer-sent-ids.txt";
        Assert.Equal(absolute, SentMessageIdStore.ResolvePath(absolute));
    }

    [Fact]
    public void ResolvePath_ReturnsEmpty_ForEmptyOrNullOrWhitespace()
    {
        Assert.Equal(string.Empty, SentMessageIdStore.ResolvePath(string.Empty));
        Assert.Equal(string.Empty, SentMessageIdStore.ResolvePath(null));
        Assert.Equal(string.Empty, SentMessageIdStore.ResolvePath("   "));
    }
}
