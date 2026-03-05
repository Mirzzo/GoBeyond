namespace GoBeyond.Infrastructure.Database;

public interface IDatabaseSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
