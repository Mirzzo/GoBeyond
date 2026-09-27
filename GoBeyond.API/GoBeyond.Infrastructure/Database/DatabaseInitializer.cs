using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Database;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>Pri pokretanju API-ja: primjena EF migracija (baza "210020") i seed demo podataka ako je baza prazna.</summary>
public sealed class DatabaseInitializer(GoBeyondDbContext db, IDatabaseSeeder seeder) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        await seeder.SeedAsync(cancellationToken);
    }
}
