using GoBeyond.Infrastructure.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Tests.TestInfrastructure;

/// <summary>
/// GoBeyondDbContext nad SQLite in-memory bazom. SQLite čuva decimal kao TEXT, pa bi SQL Server check
/// constraint-i (npr. [WeightKg] BETWEEN 30 AND 300) poredili tekst - za testove se uklanjaju
/// (ograničenja su dio SQL Server migracije).
/// </summary>
internal sealed class SqliteTestDbContext(DbContextOptions<GoBeyondDbContext> options) : GoBeyondDbContext(options)
{
    public static GoBeyondDbContext Create(SqliteConnection connection) =>
        new SqliteTestDbContext(new DbContextOptionsBuilder<GoBeyondDbContext>().UseSqlite(connection).Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        foreach (var constraint in entity.GetCheckConstraints().ToList())
            entity.RemoveCheckConstraint(constraint.ModelName);
    }
}
