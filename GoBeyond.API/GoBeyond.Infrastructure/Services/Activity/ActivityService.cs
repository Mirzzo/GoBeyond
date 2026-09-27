using GoBeyond.Core.Entities;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoBeyond.Infrastructure.Services.Activity;

public interface IActivityService
{
    Task HeartbeatAsync(int userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Mjerenje vremena provedenog na platformi. Klijenti zovu heartbeat svakih 60 s dok su aktivni.
/// Po pozivu se pripisuje vrijeme od prethodnog heartbeat-a, najviše MaxSecondsPerHeartbeat (90 s).
/// Ako je pauza duža od dvostrukog maksimuma, smatra se da je počela nova sesija (pripisuje se 0).
/// </summary>
public sealed class ActivityService(GoBeyondDbContext db, IOptions<ActivityOptions> options) : IActivityService
{
    public async Task HeartbeatAsync(int userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var max = options.Value.MaxSecondsPerHeartbeat;

        var last = await db.UserActivities.Where(x => x.UserId == userId)
            .OrderByDescending(x => x.LastHeartbeatAt).FirstOrDefaultAsync(cancellationToken);
        var credit = last is null ? 0 : CreditSeconds((now - last.LastHeartbeatAt).TotalSeconds, max);

        var row = last?.Day == today
            ? last
            : await db.UserActivities.FirstOrDefaultAsync(x => x.UserId == userId && x.Day == today, cancellationToken);
        if (row is null)
        {
            row = new UserActivity { UserId = userId, Day = today };
            db.UserActivities.Add(row);
        }

        row.ActiveSeconds += credit;
        row.LastHeartbeatAt = now;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Dva istovremena heartbeat-a kreirala su isti (UserId, Day) red - drugi se zanemaruje.
        }
    }

    public static int CreditSeconds(double elapsedSeconds, int maxSeconds)
    {
        if (elapsedSeconds <= 0) return 0;
        if (elapsedSeconds <= maxSeconds) return (int)Math.Round(elapsedSeconds);
        return elapsedSeconds <= 2 * maxSeconds ? maxSeconds : 0;
    }
}
