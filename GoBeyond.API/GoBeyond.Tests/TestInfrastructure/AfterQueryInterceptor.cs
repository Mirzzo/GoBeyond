using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GoBeyond.Tests.TestInfrastructure;

/// <summary>
/// Jednom, odmah nakon izvršenja upita čiji SQL sadrži zadani dio (npr. ciklus je upravo odabrao pretplate), izvrši
/// "istovremenu" izmjenu na drugoj konekciji. Odabrani redovi su tada već zastarjeli.
/// </summary>
internal sealed class AfterQueryInterceptor(string sqlFragment, Func<Task> concurrentChange) : DbCommandInterceptor
{
    public bool Fired { get; private set; }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (!Fired && command.CommandText.Contains(sqlFragment, StringComparison.Ordinal))
        {
            Fired = true;
            await concurrentChange();
        }
        return result;
    }
}
