using GoBeyond.Core.Entities;
using GoBeyond.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Auth;

/// <summary>
/// Završavanje sesija korisnika (reset i promjena lozinke, blokiranje, brisanje, promjena uloge). Novi security stamp odmah
/// poništava izdate access tokene (i ne oživljavaju nakon odblokiranja ili vraćanja uloge), a refresh tokeni se opozivaju.
/// Refresh token važi samo uz stamp sa kojim je izdat, pa ni token koji istovremeni refresh upiše nakon opoziva ne produžava
/// sesiju.
/// </summary>
public static class UserSessions
{
    /// <summary>
    /// Mijenja stamp (upisuje se sa sljedećim SaveChanges) i opoziva refresh tokene u tekućoj transakciji, pa se opoziv poništava
    /// zajedno sa izmjenom korisnika ako snimanje ne uspije. Sesija <paramref name="keepSessionId"/> (uređaj koji je sam promijenio
    /// lozinku) ostaje: njen refresh token dobija novi stamp, pa aplikacija nakon 401 za stari access token obnovi tokene i nastavi.
    /// </summary>
    public static async Task EndSessionsAsync(this GoBeyondDbContext db, User user, Guid? keepSessionId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Sesije se završavaju samo unutar transakcije.");

        user.SecurityStamp = Guid.NewGuid();
        var stamp = user.SecurityStamp;
        var now = DateTime.UtcNow;
        var active = db.RefreshTokens.Where(x => x.UserId == user.Id && x.RevokedAt == null);

        await active.Where(x => x.SessionId != keepSessionId)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAt, now), cancellationToken);
        if (keepSessionId is not null)
            await active.Where(x => x.SessionId == keepSessionId)
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.SecurityStamp, stamp), cancellationToken);
    }
}
