using GoBeyond.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Subscriptions;

/// <summary>
/// Zaključavanje pretplate za prelaz statusa. Svaki prelaz (plaćanje, prihvatanje, odbijanje, otkazivanje, istek) ide u
/// transakciji koja prvo zaključa red pretplate, pa tek onda čita njeno stanje i odlučuje. Istovremeni prelazi iste
/// pretplate se tako izvršavaju jedan za drugim i svaki vidi rezultat prethodnog (nema izgubljenih izmjena, npr. zahtjev
/// koji je odbijen i vraćen, a istovremeno prihvaćen). Redoslijed je uvijek pretplata pa uplate, pa nema deadlock-a.
/// </summary>
public static class SubscriptionLocks
{
    /// <summary>
    /// Zaključava red pretplate do kraja tekuće transakcije: UPDATE bez promjene (SQL Server drži X lock na redu i uz
    /// READ_COMMITTED_SNAPSHOT, a SQLite zaključava bazu za upis). Poziva se prije učitavanja pretplate (ili se učitani
    /// entitet zatim osvježi). Vraća false ako pretplata ne postoji.
    /// </summary>
    public static async Task<bool> LockSubscriptionAsync(this GoBeyondDbContext db, int subscriptionId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Pretplata se zaključava samo unutar transakcije.");

        return await db.Subscriptions.Where(x => x.Id == subscriptionId)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.Status, s => s.Status), cancellationToken) > 0;
    }
}
