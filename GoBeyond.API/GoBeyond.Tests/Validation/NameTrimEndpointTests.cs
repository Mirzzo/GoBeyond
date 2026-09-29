using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Tests.Security;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GoBeyond.Tests.Validation;

/// <summary>
/// Ime, prezime i nazivi u šifarnicima se trimuju prije provjere dužine kroz pravi API (JSON i multipart): vrijednost
/// dopunjena razmacima do minimuma vraća 400 i ništa se ne upisuje, a ispravna vrijednost sa razmacima se snima trimovana.
/// </summary>
public sealed class NameTrimEndpointTests(GoBeyondApiFactory factory) : IClassFixture<GoBeyondApiFactory>
{
    private const string FirstNameLength = "Ime mora imati 2–50 znakova.";
    private const string LastNameLength = "Prezime mora imati 2–50 znakova.";

    [Theory]
    [InlineData("A     ", "Klijent", "firstName", FirstNameLength)]
    [InlineData("Qarts", "Q        ", "lastName", LastNameLength)]
    public async Task RegisterClient_NamePaddedToMinimum_Returns400AndCreatesNoUser(string firstName, string lastName, string field,
        string message)
    {
        var username = $"trim_client_{field}";

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register/client", ClientBody(username, firstName, lastName));

        await AssertFieldErrorAsync(response, field, message);
        Assert.False(factory.Query(db => db.Users.Any(x => x.Username == username)));
    }

    [Fact]
    public async Task RegisterClient_PaddedValidNames_AreStoredTrimmed()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register/client",
            ClientBody("trim_client_ok", "  Ana  ", " Kovač   "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = factory.Query(db => db.Users.AsNoTracking().Single(x => x.Username == "trim_client_ok"));
        Assert.Equal(("Ana", "Kovač"), (user.FirstName, user.LastName));
    }

    [Fact]
    public async Task RegisterMentor_FirstNamePaddedToMinimum_Returns400AndCreatesNoUser()
    {
        var (genderId, _, goalId, typeId) = SecurityTestApi.ReferenceIds(factory);
        using var form = new MultipartFormDataContent();
        foreach (var (name, value) in new[]
                 {
                     ("firstName", "D       "), ("lastName", "Mentor"), ("username", "trim_mentor"), ("email", "trim_mentor@test.ba"),
                     ("dateOfBirth", "1990-03-03"), ("genderId", genderId.ToString()), ("password", SecurityTestApi.Password),
                     ("confirmPassword", SecurityTestApi.Password), ("trainingTypeId", typeId.ToString()), ("bio", new string('b', 60)),
                     ("yearsOfExperience", "3"), ("monthlyPrice", "15"), ("specializationIds", goalId.ToString())
                 })
            form.Add(new StringContent(value), name);
        var certificate = new ByteArrayContent("%PDF-1.4\n%%EOF\n"u8.ToArray());
        certificate.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(certificate, "certificates", "cert.pdf");

        var response = await factory.CreateClient().PostAsync("/api/auth/register/mentor", form);

        await AssertFieldErrorAsync(response, "firstName", FirstNameLength);
        Assert.False(factory.Query(db => db.Users.Any(x => x.Username == "trim_mentor")));
    }

    [Fact]
    public async Task AdminUpdateUser_LastNamePaddedToMinimum_Returns400AndKeepsTheStoredName()
    {
        var session = await SecurityTestApi.RegisterClientAsync(factory, "trim_admin_target");
        var (genderId, levelId, goalId, _) = SecurityTestApi.ReferenceIds(factory);

        var response = await factory.ClientFor(TestUsers.Admin).PutAsJsonAsync($"/api/admin/users/{session.User.Id}", new
        {
            firstName = "Test", lastName = "C      ", username = "trim_admin_target", email = "trim_admin_target@test.ba",
            dateOfBirth = "1995-05-05", genderId, role = "Client",
            client = new { weightKg = 80, heightCm = 180, fitnessLevelId = levelId, trainingExperienceYears = 1, fitnessGoalId = goalId }
        });

        await AssertFieldErrorAsync(response, "lastName", LastNameLength);
        Assert.Equal("Korisnik", factory.Query(db => db.Users.AsNoTracking().Single(x => x.Id == session.User.Id).LastName));
    }

    /// <summary>
    /// Ime od jednog znaka upisano prije ove provjere ostaje kakvo jeste (podaci se ne mijenjaju sami), ali se profil ne može
    /// snimiti dok ga korisnik ne ispravi: greška je na polju imena, kao za svaki drugi neispravan unos.
    /// </summary>
    [Fact]
    public async Task StoredOneCharacterName_StaysAndMustBeCorrectedOnTheNextProfileSave()
    {
        var session = await SecurityTestApi.RegisterClientAsync(factory, "trim_legacy_name");
        await SetFirstNameAsync(session.User.Id, "A");
        var (genderId, levelId, goalId, _) = SecurityTestApi.ReferenceIds(factory);
        object Body(string firstName) => new
        {
            firstName, lastName = "Korisnik", username = "trim_legacy_name", email = "trim_legacy_name@test.ba",
            dateOfBirth = "1995-05-05", genderId,
            client = new { weightKg = 80, heightCm = 180, fitnessLevelId = levelId, trainingExperienceYears = 1, fitnessGoalId = goalId }
        };
        var user = SecurityTestApi.WithToken(factory, session.AccessToken);

        await AssertFieldErrorAsync(await user.PutAsJsonAsync("/api/user-profile/me", Body("A")), "firstName", FirstNameLength);
        Assert.Equal("A", factory.Query(db => db.Users.AsNoTracking().Single(x => x.Id == session.User.Id).FirstName));

        Assert.Equal(HttpStatusCode.OK, (await user.PutAsJsonAsync("/api/user-profile/me", Body("Amar"))).StatusCode);
        Assert.Equal("Amar", factory.Query(db => db.Users.AsNoTracking().Single(x => x.Id == session.User.Id).FirstName));
    }

    [Theory]
    [InlineData("/api/genders", "Naziv mora imati 2–30 znakova.")]
    [InlineData("/api/training-types", "Naziv mora imati 2–50 znakova.")]
    [InlineData("/api/fitness-goals", "Naziv mora imati 2–60 znakova.")]
    [InlineData("/api/fitness-levels", "Naziv mora imati 2–40 znakova.")]
    public async Task ReferenceData_NamePaddedToMinimum_Returns400BeforeLookingUpTheItem(string route, string message)
    {
        var admin = factory.ClientFor(TestUsers.Admin);
        var body = new { name = "a     ", description = "Opis stavke", sortOrder = 5 };

        await AssertFieldErrorAsync(await admin.PutAsJsonAsync($"{route}/999999", body), "name", message);
        await AssertFieldErrorAsync(await admin.PostAsJsonAsync(route, body), "name", message);
    }

    [Fact]
    public async Task ReferenceData_PaddedValidNameAndDescription_AreStoredTrimmed()
    {
        var response = await factory.ClientFor(TestUsers.Admin).PostAsJsonAsync("/api/training-types",
            new { name = "  Pilates  ", description = "  Vježbe za core i fleksibilnost  " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = factory.Query(db => db.TrainingTypes.AsNoTracking().Single(x => x.Name == "Pilates"));
        Assert.Equal("Vježbe za core i fleksibilnost", saved.Description);
    }

    private object ClientBody(string username, string firstName, string lastName)
    {
        var (genderId, levelId, goalId, _) = SecurityTestApi.ReferenceIds(factory);
        return new
        {
            firstName, lastName, username, email = $"{username}@test.ba", dateOfBirth = "1995-05-05", genderId,
            password = SecurityTestApi.Password, confirmPassword = SecurityTestApi.Password, weightKg = 80, heightCm = 180,
            fitnessLevelId = levelId, trainingExperienceYears = 1, fitnessGoalId = goalId
        };
    }

    private async Task SetFirstNameAsync(int userId, string firstName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GoBeyondDbContext>();
        await db.Users.Where(x => x.Id == userId).ExecuteUpdateAsync(x => x.SetProperty(u => u.FirstName, firstName));
    }

    private static async Task AssertFieldErrorAsync(HttpResponseMessage response, string field, string message)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await SecurityTestApi.ErrorAsync(response);
        Assert.NotNull(error.Errors);
        Assert.Contains(message, error.Errors![field]);
    }
}
