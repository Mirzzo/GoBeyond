using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Utilities;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Database;

public class DatabaseSeeder(
    GoBeyondDbContext dbContext,
    IPasswordHasherService passwordHasherService) : IDatabaseSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var admin = new User
        {
            FirstName = "System",
            LastName = "Admin",
            Email = "admin@gobeyond.local",
            PasswordHash = passwordHasherService.Hash("Admin123!"),
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
