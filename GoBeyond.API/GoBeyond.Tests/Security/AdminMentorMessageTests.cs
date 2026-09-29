using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Admin;
using GoBeyond.Tests.Payments;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;

namespace GoBeyond.Tests.Security;

/// <summary>Poruke odobravanja/odbijanja mentorskog naloga ne smiju pretpostavljati muški rod (npr. "Mentor Selma Delić je odobren").</summary>
public sealed class AdminMentorMessageTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public AdminMentorMessageTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Approve_UsesGenderNeutralMessage()
    {
        await using var db = SqliteTestDbContext.Create(_connection);
        var mentorId = await SeedPendingMentorAsync(db, "selma.test");

        var result = await new AdminMentorService(db, new RecordingNotificationSender()).ApproveAsync(mentorId);

        Assert.Equal("Mentorski nalog (Selma Delić) je odobren.", result.Message);
    }

    [Fact]
    public async Task Reject_UsesGenderNeutralMessage()
    {
        await using var db = SqliteTestDbContext.Create(_connection);
        var mentorId = await SeedPendingMentorAsync(db, "selma.test2");

        var result = await new AdminMentorService(db, new RecordingNotificationSender())
            .RejectAsync(mentorId, new RejectRequest { Reason = "Certifikat nije čitljiv." });

        Assert.Equal("Zahtjev za mentorski nalog (Selma Delić) je odbijen.", result.Message);
    }

    private static async Task<int> SeedPendingMentorAsync(GoBeyondDbContext db, string username)
    {
        await db.Database.EnsureCreatedAsync();
        var mentor = new MentorProfile
        {
            User = new User
            {
                FirstName = "Selma", LastName = "Delić", Username = username, Email = $"{username}@test.ba",
                DateOfBirth = new DateOnly(1990, 1, 1), Gender = new Gender { Name = $"Žensko {username}" }, PasswordHash = "x",
                Role = UserRole.Mentor
            },
            TrainingType = new TrainingType { Name = $"Yoga {username}", Description = "Joga" },
            Bio = new string('b', 60), YearsOfExperience = 4, MonthlyPrice = 20, Status = MentorApprovalStatus.Pending
        };
        db.MentorProfiles.Add(mentor);
        await db.SaveChangesAsync();
        return mentor.Id;
    }
}
