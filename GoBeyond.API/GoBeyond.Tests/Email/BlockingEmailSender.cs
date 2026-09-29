using GoBeyond.EmailConsumer.Services;

namespace GoBeyond.Tests.Email;

/// <summary>
/// <see cref="IEmailSender"/> koji broji pozive i "šalje" tek kad test pozove <see cref="Release"/> - stoji
/// umjesto sporog SMTP odgovora, bez stvarnog I/O-a i bez sleep-a. Nastavci nakon slanja se izvršavaju odmah
/// unutar <see cref="Release"/> (ili se predaju SynchronizationContext-u koji ih je uhvatio), pa test tačno zna
/// dokle je koji handler stigao.
/// </summary>
internal sealed class BlockingEmailSender : IEmailSender
{
    private int _callCount;
    private readonly TaskCompletionSource _release = new();

    public int CallCount => Volatile.Read(ref _callCount);

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _callCount);
        return _release.Task;
    }

    /// <summary>Svi dosadašnji i budući pozivi <see cref="SendAsync"/> "uspiju".</summary>
    public void Release() => _release.TrySetResult();
}
