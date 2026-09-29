using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Admin;
using GoBeyond.Infrastructure.Services.Notifications;
using GoBeyond.Tests.Payments;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Tests.Security;

/// <summary>
/// Poruke odobravanja/odbijanja mentorskog naloga ne smiju pretpostavljati muški rod (npr. "Mentor Selma Delić je odobren"), a
/// razlog odbijanja u obavijesti i emailu je rečenica sa završnom tačkom.
/// </summary>
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

    [Theory]
    [InlineData("  Nedovoljno dokumentacije za odobrenje  ", "Nedovoljno dokumentacije za odobrenje.")]
    [InlineData("Certifikat nije čitljiv.", "Certifikat nije čitljiv.")]
    [InlineData("Zašto nema licence?", "Zašto nema licence?")]
    public async Task Reject_NotificationAndEmailEndTheReasonWithAPeriod(string reason, string sentence)
    {
        await using var db = SqliteTestDbContext.Create(_connection);
        var mentorId = await SeedPendingMentorAsync(db, "selma.reason");

        await new AdminMentorService(db, new NotificationSender(db)).RejectAsync(mentorId, new RejectRequest { Reason = reason });

        var expected = $"Vaš zahtjev za mentorski nalog je odbijen. Razlog: {sentence}";
        var notification = await db.Notifications.AsNoTracking().SingleAsync(x => x.Type == NotificationType.MentorRejected);
        Assert.Equal(expected, notification.Body);
        var email = await db.OutboxMessages.AsNoTracking().SingleAsync(x => x.EventType == nameof(NotificationType.MentorRejected));
        Assert.Equal($"Pozdrav Selma,\n\n{expected}\n\nVaš GoBeyond tim", email.Body);
        // Sačuvani razlog ostaje kako ga je administrator upisao (prijava ga prikazuje iza dvotačke).
        Assert.Equal(reason.Trim(), await db.MentorProfiles.Where(x => x.Id == mentorId).Select(x => x.RejectionReason).SingleAsync());
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
