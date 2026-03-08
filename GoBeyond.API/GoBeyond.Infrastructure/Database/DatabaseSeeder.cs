using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Utilities;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Database;

public class DatabaseSeeder(
    GoBeyondDbContext dbContext,
    IPasswordHasherService passwordHasherService) : IDatabaseSeeder
{
    private const string AdminEmail = "admin@gobeyond.local";
    private const string AdminPassword = "Admin123!";
    private const string MentorEmail = "mentor@gobeyond.local";
    private const string MentorPassword = "Mentor123!";
    private const string ClientEmail = "client@gobeyond.local";
    private const string ClientPassword = "Client123!";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureUserAsync(
            email: AdminEmail,
            password: AdminPassword,
            firstName: "System",
            lastName: "Admin",
            role: UserRole.Admin,
            cancellationToken);

        var mentor = await EnsureUserAsync(
            email: MentorEmail,
            password: MentorPassword,
            firstName: "Mila",
            lastName: "Mentor",
            role: UserRole.Mentor,
            cancellationToken);

        mentor.MentorProfile ??= new MentorProfile();
        mentor.MentorProfile.Bio = "Certified strength and conditioning coach.";
        mentor.MentorProfile.Age = 29;
        mentor.MentorProfile.Category = MentorCategory.Hybrid;
        mentor.MentorProfile.Price = 19.99m;
        mentor.MentorProfile.Status = MentorApprovalStatus.Approved;

        var client = await EnsureUserAsync(
            email: ClientEmail,
            password: ClientPassword,
            firstName: "Luka",
            lastName: "Client",
            role: UserRole.Client,
            cancellationToken);

        client.ClientProfile ??= new ClientProfile();
        client.ClientProfile.Weight = 78.5m;
        client.ClientProfile.Height = 182m;
        client.ClientProfile.Age = 25;
        client.ClientProfile.FitnessLevel = "Intermediate";

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<User> EnsureUserAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .Include(x => x.MentorProfile)
            .Include(x => x.ClientProfile)
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                FirstName = firstName,
                LastName = lastName,
                Email = normalizedEmail,
                PasswordHash = passwordHasherService.Hash(password),
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Users.Add(user);
            return user;
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Role = role;
        user.IsActive = true;

        if (!passwordHasherService.Verify(password, user.PasswordHash))
        {
            user.PasswordHash = passwordHasherService.Hash(password);
        }

        return user;
    }
}
