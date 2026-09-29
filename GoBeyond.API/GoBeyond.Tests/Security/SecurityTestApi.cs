using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Tests.Security;

public sealed record AuthUserBody(int Id, string Username, string Role);

public sealed record AuthBody(string AccessToken, string RefreshToken, AuthUserBody User);

public sealed record ErrorBody(string Message, Dictionary<string, string[]>? Errors);

/// <summary>Pomoćne metode za integracijske testove sesija: registracija i prijava kroz pravi API (prave lozinke i tokeni).</summary>
public static class SecurityTestApi
{
    public const string Password = "Lozinka123";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static async Task<AuthBody> RegisterClientAsync(GoBeyondApiFactory factory, string username)
    {
        var (genderId, levelId, goalId, _) = ReferenceIds(factory);
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register/client", new
        {
            firstName = "Test",
            lastName = "Korisnik",
            username,
            email = $"{username}@test.ba",
            dateOfBirth = "1995-05-05",
            genderId,
            password = Password,
            confirmPassword = Password,
            weightKg = 80,
            heightCm = 180,
            fitnessLevelId = levelId,
            trainingExperienceYears = 1,
            fitnessGoalId = goalId
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AuthBody>(Json))!;
    }

    public static async Task<AuthBody> LoginAsync(GoBeyondApiFactory factory, string username, string password = Password)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username, password });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AuthBody>(Json))!;
    }

    public static HttpClient WithToken(GoBeyondApiFactory factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static Task<HttpResponseMessage> RefreshAsync(GoBeyondApiFactory factory, string refreshToken) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken });

    public static async Task<ErrorBody> ErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ErrorBody>(Json))!;

    public static (int GenderId, int FitnessLevelId, int FitnessGoalId, int TrainingTypeId) ReferenceIds(GoBeyondApiFactory factory) =>
        factory.Query(db => (
            db.Genders.AsNoTracking().Select(x => x.Id).First(),
            db.FitnessLevels.AsNoTracking().Select(x => x.Id).First(),
            db.FitnessGoals.AsNoTracking().Select(x => x.Id).First(),
            db.TrainingTypes.AsNoTracking().Select(x => x.Id).First()));

    /// <summary>Tijelo za PUT /api/admin/users/{id} (ista lična polja kao pri registraciji, zadana uloga).</summary>
    public static object AdminUpdateBody(GoBeyondApiFactory factory, string username, string role, bool withMentor = false)
    {
        var (genderId, levelId, goalId, typeId) = ReferenceIds(factory);
        return new
        {
            firstName = "Test",
            lastName = "Korisnik",
            username,
            email = $"{username}@test.ba",
            dateOfBirth = "1995-05-05",
            genderId,
            role,
            mentor = withMentor
                ? new
                {
                    trainingTypeId = typeId,
                    nickname = username,
                    bio = new string('b', 60),
                    yearsOfExperience = 3,
                    monthlyPrice = 25,
                    specializationIds = new[] { goalId }
                }
                : null,
            client = role == "Client"
                ? new { weightKg = 80, heightCm = 180, fitnessLevelId = levelId, trainingExperienceYears = 1, fitnessGoalId = goalId }
                : null
        };
    }
}
