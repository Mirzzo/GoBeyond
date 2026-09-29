using System.Collections.Concurrent;

namespace GoBeyond.EmailConsumer.Services;

/// <summary>
/// Sprječava dupli SMTP send kad broker redelivera istu poruku dok je prvi pokušaj još u toku (npr. konekcija
/// pukne i RabbitMQ.Client je automatski oporavi dok je prvi handler i dalje blokiran na sporom SMTP odgovoru).
/// <see cref="SentMessageIdStore"/> ovo ne pokriva jer se upisuje tek NAKON uspješnog slanja. Poziv za ključ koji
/// je već u toku čeka ishod tog poziva umjesto da šalje ponovo, i pokuša sam samo ako je prvi propao.
/// </summary>
public sealed class InFlightSendGate
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _inFlight = new();

    /// <summary>
    /// Vraća true ako je OVAJ poziv stvarno izvršio <paramref name="attempt"/> (prvi ili nakon što je
    /// prethodni za isti ključ propao); false ako je konkurentni poziv za isti ključ u međuvremenu već
    /// uspješno poslao email - tada se ovaj poziv samo ack-uje bez ponovnog slanja.
    /// </summary>
    public async Task<bool> RunAsync(string key, Func<Task> attempt)
    {
        while (true)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_inFlight.TryAdd(key, tcs))
            {
                try
                {
                    await attempt().ConfigureAwait(false);
                    tcs.TrySetResult(true);
                    return true;
                }
                catch
                {
                    tcs.TrySetResult(false);
                    throw;
                }
                finally
                {
                    _inFlight.TryRemove(new KeyValuePair<string, TaskCompletionSource<bool>>(key, tcs));
                }
            }

            if (_inFlight.TryGetValue(key, out var existing) && await existing.Task.ConfigureAwait(false))
                return false;
            // ključ više nije u toku ili je prethodni pokušaj propao - probaj sam preuzeti slanje
        }
    }
}
