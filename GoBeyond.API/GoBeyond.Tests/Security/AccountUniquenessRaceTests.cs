using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.DTOs.Auth;
using GoBeyond.Core.DTOs.Profile;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.Files;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Security;
using GoBeyond.Infrastructure.Services.Admin;
using GoBeyond.Infrastructure.Services.Auth;
using GoBeyond.Infrastructure.Services.Files;
using GoBeyond.Infrastructure.Services.Users;
using GoBeyond.Tests.Payments;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Security;

/// <summary>
/// Dva istovremena zahtjeva sa istim korisničkim imenom/emailom: oba prođu provjeru prije upisa, a jedinstveni indeks
/// odbije drugi upis. Rezultat mora biti ista 400 greška po polju kao kod sekvencijalnog zahtjeva, nikad 500.
/// Utrka se simulira validatorom koji odmah nakon (uspješne) provjere upiše "konkurentskog" korisnika.
/// </summary>
public sealed class AccountUniquenessRaceTests : IDisposable
{
    private const string UsernameTaken = "Korisničko ime je već zauzeto.";
    private const string EmailTaken = "Email adresa je već registrovana.";

    private static readonly JwtOptions Jwt = new()
    {
        SecretKey = "GoBeyond_Test_Secret_Key_For_Uniqueness_Race_2026", Issuer = "GoBeyond", Audience = "GoBeyondClients",
        AccessTokenLifetimeMinutes = 60, RefreshTokenLifetimeDays = 14
    };

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly int _genderId, _levelId, _goalId, _typeId;

    public AccountUniquenessRaceTests()
    {
        _connection.Open();
        using var db = CreateContext();
        db.Database.EnsureCreated();
        var gender = new Gender { Name = "Muško" };
        var level = new FitnessLevel { Name = "Početnik", SortOrder = 1 };
        var goal = new FitnessGoal { Name = "Snaga" };
        var type = new TrainingType { Name = "Weightlifting", Description = "Utezi" };
        db.AddRange(gender, level, goal, type);
        db.SaveChanges();
        (_genderId, _levelId, _goalId, _typeId) = (gender.Id, level.Id, goal.Id, type.Id);
    }

    public void Dispose() => _connection.Dispose();

    [Theory]
    [InlineData("username", UsernameTaken)]
    [InlineData("email", EmailTaken)]
    public async Task RegisterClient_LosingConcurrentRace_Returns400FieldError(string field, string message)
    {
        await using var db = CreateContext();
        var request = ClientRequest("race_client", "race_client@test.ba");
        var validator = new RacingValidator(db, () => InsertCompetitor(field == "username" ? "race_client" : "other_1",
            field == "email" ? "race_client@test.ba" : "other_1@test.ba"));

        var error = await Assert.ThrowsAsync<ValidationException>(() => CreateAuthService(db, validator).RegisterClientAsync(request));

        Assert.Equal([message], error.Errors[field]);
        Assert.Equal(1, await CreateContext().Users.CountAsync());
    }

    [Fact]
    public async Task RegisterMentor_LosingConcurrentRace_Returns400AndDeletesSavedCertificates()
    {
        await using var db = CreateContext();
        var files = new FakeFileStorage();
        var request = new RegisterMentorRequest
        {
            FirstName = "Utrka", LastName = "Mentor", Username = "race_mentor", Email = "race_mentor@test.ba",
            DateOfBirth = new DateOnly(1990, 1, 1), GenderId = _genderId, Password = "Lozinka123", ConfirmPassword = "Lozinka123",
            TrainingTypeId = _typeId, Bio = new string('b', 60), YearsOfExperience = 3, MonthlyPrice = 20, SpecializationIds = [_goalId]
        };
        var validator = new RacingValidator(db, () => InsertCompetitor("race_mentor", "other_2@test.ba"));
        var certificate = new FileUpload("certifikat.pdf", "application/pdf", 4, () => new MemoryStream("%PDF"u8.ToArray()));

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateAuthService(db, validator, files).RegisterMentorAsync(request, [certificate]));

        Assert.Equal([UsernameTaken], error.Errors["username"]);
        Assert.Equal(files.Saved, files.Deleted);
        Assert.Single(files.Saved);
    }

    [Fact]
    public async Task AdminUpdateUser_LosingConcurrentRaceOnEmail_Returns400FieldError()
    {
        int userId;
        await using (var seed = CreateContext())
        {
            var user = NewUser("race_admin_target", "race_admin_target@test.ba");
            user.ClientProfile = new ClientProfile { WeightKg = 80, HeightCm = 180, FitnessLevelId = _levelId, FitnessGoalId = _goalId };
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
            userId = user.Id;
        }

        await using var db = CreateContext();
        var validator = new RacingValidator(db, () => InsertCompetitor("other_3", "novi@test.ba"));
        var service = new AdminUserService(db, validator, new PasswordHasher(), null!);
        var request = new AdminUpdateUserRequest
        {
            FirstName = "Test", LastName = "Korisnik", Username = "race_admin_target", Email = "novi@test.ba",
            DateOfBirth = new DateOnly(1990, 1, 1), GenderId = _genderId, Role = UserRole.Client
        };

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.UpdateUserAsync(adminUserId: 0, userId, request));

        Assert.Equal([EmailTaken], error.Errors["email"]);
    }

    [Fact]
    public async Task AdminRoleChange_LosingConcurrentRace_Returns400AndKeepsSessions()
    {
        int userId;
        Guid stamp;
        await using (var seed = CreateContext())
        {
            var user = NewUser("race_role_target", "race_role_target@test.ba");
            user.ClientProfile = new ClientProfile { WeightKg = 80, HeightCm = 180, FitnessLevelId = _levelId, FitnessGoalId = _goalId };
            user.RefreshTokens.Add(new RefreshToken
            {
                TokenHash = "race-role-token", SessionId = Guid.NewGuid(), SecurityStamp = user.SecurityStamp, ExpiresAt = DateTime.UtcNow.AddDays(14)
            });
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
            (userId, stamp) = (user.Id, user.SecurityStamp);
        }

        await using var db = CreateContext();
        var validator = new RacingValidator(db, () => InsertCompetitor("other_4", "zauzet@test.ba"));
        var service = new AdminUserService(db, validator, new PasswordHasher(), null!);
        var request = new AdminUpdateUserRequest
        {
            FirstName = "Test", LastName = "Korisnik", Username = "race_role_target", Email = "zauzet@test.ba",
            DateOfBirth = new DateOnly(1990, 1, 1), GenderId = _genderId, Role = UserRole.Admin
        };

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.UpdateUserAsync(adminUserId: 0, userId, request));

        Assert.Equal([EmailTaken], error.Errors["email"]);
        await using var check = CreateContext();
        var saved = await check.Users.AsNoTracking().SingleAsync(x => x.Id == userId);
        Assert.Equal(UserRole.Client, saved.Role);
        Assert.Equal(stamp, saved.SecurityStamp);
        Assert.True(await check.RefreshTokens.AnyAsync(x => x.UserId == userId && x.RevokedAt == null));
    }

    [Fact]
    public async Task UnrelatedSaveFailure_IsNotReportedAsTakenAccount()
    {
        await using var db = CreateContext();
        // Nepostojeći spol prođe provjeru (validator ga "ne vidi"), pa upis padne na stranom ključu, ne na jedinstvenosti.
        var request = ClientRequest("race_fk", "race_fk@test.ba");
        request.GenderId = 999;
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            CreateAuthService(db, new RacingValidator(db, () => Task.CompletedTask, skipChecks: true)).RegisterClientAsync(request));
    }

    private RegisterClientRequest ClientRequest(string username, string email) => new()
    {
        FirstName = "Utrka", LastName = "Klijent", Username = username, Email = email, DateOfBirth = new DateOnly(1995, 5, 5),
        GenderId = _genderId, Password = "Lozinka123", ConfirmPassword = "Lozinka123", WeightKg = 80, HeightCm = 180,
        FitnessLevelId = _levelId, TrainingExperienceYears = 1, FitnessGoalId = _goalId
    };

    private User NewUser(string username, string email) => new()
    {
        FirstName = "Konkurent", LastName = "Test", Username = username, Email = email, DateOfBirth = new DateOnly(1990, 1, 1),
        GenderId = _genderId, PasswordHash = "x", Role = UserRole.Client
    };

    /// <summary>"Drugi zahtjev" koji je upisao korisnika nakon naše provjere, a prije našeg upisa.</summary>
    private async Task InsertCompetitor(string username, string email)
    {
        await using var other = CreateContext();
        other.Users.Add(NewUser(username, email));
        await other.SaveChangesAsync();
    }

    private GoBeyondDbContext CreateContext() => SqliteTestDbContext.Create(_connection);

    private static AuthService CreateAuthService(GoBeyondDbContext db, IUserAccountValidator validator, IFileStorageService? files = null) =>
        new(db, new PasswordHasher(), new JwtTokenService(Options.Create(Jwt)), validator, files ?? new FakeFileStorage(),
            new RecordingNotificationSender(), Options.Create(Jwt), Options.Create(new UploadOptions { MaxCertificatesPerUpload = 5 }));

    /// <summary>Pravi validator, ali nakon provjere računa pusti "konkurentski" upis.</summary>
    private sealed class RacingValidator(GoBeyondDbContext db, Func<Task> competitor, bool skipChecks = false) : IUserAccountValidator
    {
        private readonly UserAccountValidator _inner = new(db);

        public async Task ValidateAccountAsync(ValidationErrorCollector errors, AccountFieldsRequest request, int? currentUserId,
            CancellationToken cancellationToken)
        {
            if (!skipChecks) await _inner.ValidateAccountAsync(errors, request, currentUserId, cancellationToken);
            await competitor();
        }

        public Task ValidateMentorAsync(ValidationErrorCollector errors, int trainingTypeId, IReadOnlyCollection<int> specializationIds,
            string prefix, CancellationToken cancellationToken) =>
            _inner.ValidateMentorAsync(errors, trainingTypeId, specializationIds, prefix, cancellationToken);

        public Task ValidateClientAsync(ValidationErrorCollector errors, int fitnessLevelId, int fitnessGoalId, int? preferredTrainingTypeId,
            string prefix, CancellationToken cancellationToken) =>
            skipChecks ? Task.CompletedTask : _inner.ValidateClientAsync(errors, fitnessLevelId, fitnessGoalId, preferredTrainingTypeId, prefix, cancellationToken);

        public Task ThrowIfAccountTakenAsync(AccountFieldsRequest request, int? currentUserId, CancellationToken cancellationToken) =>
            _inner.ThrowIfAccountTakenAsync(request, currentUserId, cancellationToken);
    }

    private sealed class FakeFileStorage : IFileStorageService
    {
        public List<string> Saved { get; } = [];
        public List<string> Deleted { get; } = [];

        public Task<string> SavePublicAsync(FileUpload file, string category, UploadKind kind, string fieldName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string> SavePrivateAsync(FileUpload file, string category, UploadKind kind, string fieldName, CancellationToken cancellationToken = default)
        {
            var location = $"private://{category}/{Guid.NewGuid():N}.pdf";
            Saved.Add(location);
            return Task.FromResult(location);
        }

        public void Validate(FileUpload file, UploadKind kind, string fieldName)
        {
        }

        public string? ResolvePrivatePath(string location) => null;

        public void Delete(string? location)
        {
            if (location is not null) Deleted.Add(location);
        }
    }
}
