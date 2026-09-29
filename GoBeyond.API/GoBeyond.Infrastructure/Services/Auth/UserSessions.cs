using GoBeyond.Core.Entities;
using GoBeyond.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Auth;

/// <summary>
/// Završavanje sesija korisnika (reset i promjena lozinke, blokiranje, brisanje, promjena uloge). Novi security stamp odmah
/// poništava izdate access tokene (i ne oživljavaju nakon odblokiranja ili vraćanja uloge), a refresh tokeni se opozivaju.
/// Refresh token važi samo uz stamp sa kojim je izdat, pa ni token koji istovremeni refresh upiše nakon opoziva ne produžava
/// sesiju.
/// <para>
/// Svaka izmjena lozinke, stampa ili statusa naloga zaključa red korisnika (<see cref="LockUserAsync"/>) prije nego što
/// pročita korisnika i odluči. Istovremene izmjene istog korisnika se tako izvršavaju jedna za drugom i svaka vidi rezultat
/// prethodne: admin reset se ne gubi zbog promjene lozinke provjerene starom lozinkom, a od dvije istovremene promjene
/// uspijeva jedna. Redoslijed zaključavanja je uvijek profil korisnika ili pretplate, pa korisnik, pa njegovi refresh tokeni:
/// isti kao kod izmjene vlastitog profila, odobravanja mentora i prelaza pretplata (koji upisuju profil ili pretplatu, pa
/// korisnika ili obavijest za njega), pa nema deadlock-a.
/// </para>
/// </summary>
public static class UserSessions
{
    /// <summary>
    /// Zaključava red korisnika do kraja tekuće transakcije: UPDATE bez promjene (SQL Server drži X lock na redu i uz
    /// READ_COMMITTED_SNAPSHOT, a SQLite zaključava bazu za upis). Poziva se prije učitavanja korisnika (ili se učitani
    /// entitet zatim osvježi). Vraća false ako korisnik ne postoji.
    /// </summary>
    public static async Task<bool> LockUserAsync(this GoBeyondDbContext db, int userId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Korisnik se zaključava samo unutar transakcije.");

        return await db.Users.Where(x => x.Id == userId)
            .ExecuteUpdateAsync(x => x.SetProperty(u => u.SecurityStamp, u => u.SecurityStamp), cancellationToken) > 0;
    }

    /// <summary>
    /// Mijenja stamp (upisuje se sa sljedećim SaveChanges) i opoziva refresh tokene u tekućoj transakciji, pa se opoziv poništava
    /// zajedno sa izmjenom korisnika ako snimanje ne uspije. Sesija <paramref name="keepSessionId"/> (uređaj koji je sam promijenio
    /// lozinku) ostaje: njen refresh token dobija novi stamp, pa aplikacija nakon 401 za stari access token obnovi tokene i nastavi.
    /// Red korisnika mora već biti zaključan (<see cref="LockUserAsync"/>).
    /// </summary>
    public static async Task EndSessionsAsync(this GoBeyondDbContext db, User user, Guid? keepSessionId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Sesije se završavaju samo unutar transakcije.");

        user.SecurityStamp = Guid.NewGuid();
        var stamp = user.SecurityStamp;
        var now = DateTime.UtcNow;

        // Važeći tokeni se pročitaju bez zaključavanja i mijenjaju pojedinačno po Id-u. UPDATE po svim tokenima korisnika bi
        // čekao i na token koji istovremeni refresh ili prijava upravo upisuje, a taj upis (strani ključ) čeka na ovaj zaključani
        // red korisnika: deadlock. Tako kasno upisan token nosi stari stamp i ionako ne važi, a istekle tokene briše izdavanje
        // novih (AuthService), pa se ovdje ne diraju.
        var tokens = await db.RefreshTokens.AsNoTracking()
            .Where(x => x.UserId == user.Id && x.RevokedAt == null && x.ExpiresAt > now)
            .Select(x => new { x.Id, x.SessionId })
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            var row = db.RefreshTokens.Where(x => x.Id == token.Id && x.RevokedAt == null);
            if (keepSessionId is not null && token.SessionId == keepSessionId)
                await row.ExecuteUpdateAsync(x => x.SetProperty(t => t.SecurityStamp, stamp), cancellationToken);
            else
                await row.ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAt, now), cancellationToken);
        }
    }
}
