using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Mentors;
using GoBeyond.Infrastructure.Services.Recommendations;
using GoBeyond.Tests.Payments;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;

namespace GoBeyond.Tests.Recommendations;

/// <summary>Razlozi preporuke su rodno neutralni (i za mentoricu), a broj godina iskustva ima ispravan oblik.</summary>
public sealed class RecommendationReasonTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly GoBeyondDbContext _db;
    private readonly RecommendationService _service;
    private readonly Gender _gender = new() { Name = "Žensko" };
    private readonly TrainingType _type = new() { Name = "Weightlifting", Description = "Utezi" };
    private readonly FitnessGoal _goal = new() { Name = "Povećanje mišićne mase" };

    public RecommendationReasonTests()
    {
        _connection.Open();
        _db = SqliteTestDbContext.Create(_connection);
        _db.Database.EnsureCreated();
        _service = new RecommendationService(_db, new MentorCatalogService(_db, new FakePaymentGateway()));
    }

    public void Dispose() => _connection.Dispose();

    private User NewUser(string first, string last, UserRole role) => new()
    {
        FirstName = first, LastName = last, Username = $"{first}.{last}".ToLowerInvariant(), Email = $"{first}@test.ba".ToLowerInvariant(),
        DateOfBirth = new DateOnly(1990, 1, 1), Gender = _gender, PasswordHash = "x", Role = role
    };

    private MentorProfile AddMentor(string first, string last, int years)
    {
        var mentor = new MentorProfile
        {
            User = NewUser(first, last, UserRole.Mentor), TrainingType = _type, Bio = new string('b', 60), YearsOfExperience = years,
            MonthlyPrice = 20, Status = MentorApprovalStatus.Approved
        };
        mentor.Specializations.Add(new MentorSpecialization { FitnessGoal = _goal });
        _db.MentorProfiles.Add(mentor);
        return mentor;
    }

    private User AddClient()
    {
        var client = new ClientProfile
        {
            User = NewUser("Nađa", "Škrijelj", UserRole.Client), WeightKg = 60, HeightCm = 170,
            FitnessLevel = new FitnessLevel { Name = "Početnik", SortOrder = 1 }, FitnessGoal = _goal,
            PreferredTrainingType = _type, TrainingExperienceYears = 1
        };
        _db.ClientProfiles.Add(client);
        return client.User;
    }

    [Fact]
    public async Task RecommendForClientAsync_ReasonsAreGenderNeutralWithCorrectYearForms()
    {
        var lejla = AddMentor("Lejla", "Mujić", years: 22);
        var selma = AddMentor("Selma", "Delić", years: 21);
        var amra = AddMentor("Amra", "Hodžić", years: 5);
        var novice = AddMentor("Dina", "Kovač", years: 3);
        var client = AddClient();
        await _db.SaveChangesAsync();

        var reasons = (await _service.RecommendForClientAsync(client.Id, take: 10))
            .ToDictionary(x => x.Mentor.MentorProfileId, x => x.Reasons);

        Assert.Equal(
            ["Vrsta treninga koju preferirate: Weightlifting", "Specijalizacija: Povećanje mišićne mase", "22 godine iskustva"],
            reasons[lejla.Id]);
        Assert.Contains("21 godina iskustva", reasons[selma.Id]);
        Assert.Contains("5 godina iskustva", reasons[amra.Id]);
        Assert.DoesNotContain(reasons[novice.Id], x => x.Contains("iskustva"));
        Assert.All(reasons.Values.SelectMany(x => x), x => Assert.DoesNotContain("Specijalizovan", x));
    }

    [Theory]
    [InlineData(1, "1 godina")]
    [InlineData(2, "2 godine")]
    [InlineData(4, "4 godine")]
    [InlineData(5, "5 godina")]
    [InlineData(11, "11 godina")]
    [InlineData(12, "12 godina")]
    [InlineData(14, "14 godina")]
    [InlineData(21, "21 godina")]
    [InlineData(22, "22 godine")]
    [InlineData(24, "24 godine")]
    [InlineData(25, "25 godina")]
    [InlineData(32, "32 godine")]
    [InlineData(112, "112 godina")]
    public void Years_UsesTheFormThatMatchesTheNumber(int count, string expected) =>
        Assert.Equal(expected, DomainTexts.Years(count));
}
