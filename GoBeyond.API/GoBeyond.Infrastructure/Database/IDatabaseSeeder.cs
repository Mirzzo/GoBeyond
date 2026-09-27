namespace GoBeyond.Infrastructure.Database;

/// <summary>Puni šifarnike i (samo u praznu bazu) demo podatke.</summary>
public interface IDatabaseSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
