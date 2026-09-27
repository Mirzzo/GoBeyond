using GoBeyond.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.API.Extensions;

public static class DatabaseInitializationExtensions
{
    private const int MaxAttempts = 12;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Pri pokretanju primjenjuje migracije (baza "210020") i puni demo podatke ako je baza prazna.
    /// SQL Server u Dockeru može biti "healthy" prije nego što prima konekcije, pa se pokušava više puta.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GoBeyondDbContext>();
                await db.Database.MigrateAsync();
                await scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>().SeedAsync();
                logger.LogInformation("Database is migrated and seeded.");
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsTransient(ex))
            {
                logger.LogWarning("Database not ready (attempt {Attempt}/{Max}): {Message}", attempt, MaxAttempts, ex.Message);
                await Task.Delay(RetryDelay);
            }
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is Microsoft.Data.SqlClient.SqlException or TimeoutException ||
        ex.InnerException is Microsoft.Data.SqlClient.SqlException;
}
