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
    /// Izvršava <paramref name="attempt"/> kao jedini pokušaj u toku za <paramref name="key"/>. Pokušaj vraća
    /// true ako je poslao email, false ako je utvrdio da je već poslan; ključ se oslobađa tek kad pokušaj
    /// završi, pa sve što pokušaj uradi nakon slanja (npr. upis u store) vide i kasniji pozivi. Vraća rezultat
    /// pokušaja, ili false ako je konkurentni poziv za isti ključ u međuvremenu uspješno završio.
    /// </summary>
    public async Task<bool> RunAsync(string key, Func<Task<bool>> attempt)
    {
        while (true)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_inFlight.TryAdd(key, tcs))
            {
                try
                {
                    var sent = await attempt().ConfigureAwait(false);
                    tcs.TrySetResult(true);
                    return sent;
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
