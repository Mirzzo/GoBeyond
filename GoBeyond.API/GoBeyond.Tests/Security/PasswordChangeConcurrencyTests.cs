using System.Data.Common;
using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.DTOs.Auth;
using GoBeyond.Core.DTOs.Common;
using GoBeyond.Core.DTOs.Profile;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Security;
using GoBeyond.Infrastructure.Services.Admin;
using GoBeyond.Infrastructure.Services.Auth;
using GoBeyond.Infrastructure.Services.Users;
using GoBeyond.Tests.Payments;
using GoBeyond.Tests.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Security;

/// <summary>
/// Promjena lozinke, admin reset, blokiranje, odblokiranje, brisanje i promjena uloge istog korisnika se izvršavaju jedna za
/// drugom nad zaključanim redom korisnika, i svaka vidi rezultat prethodne. Promjena lozinke provjerena lozinkom koja je u
/// međuvremenu zamijenjena se odbija (admin reset se ne gubi), a od dvije istovremene promjene uspijeva tačno jedna. Utrke se
/// reprodukuju deterministički: drugi zahtjev se na svojoj konekciji kompletno izvrši neposredno prije prvog upisa prvog zahtjeva.
/// </summary>
public sealed class PasswordChangeConcurrencyTests : IAsyncLifetime
{
    private const string Password = "Lozinka123";
    private const string WrongCurrentPassword = "Trenutna lozinka nije ispravna.";

    private static readonly JwtOptions Jwt = new()
    {
        SecretKey = "GoBeyond_Test_Secret_Key_For_Password_Races_2026", Issuer = "GoBeyond", Audience = "GoBeyondClients",
        AccessTokenLifetimeMinutes = 60, RefreshTokenLifetimeDays = 14
    };

    private readonly SubscriptionTestDatabase _db = new();
    private readonly PasswordHasher _hasher = new();
    private readonly Guid _sessionA = Guid.NewGuid();
    private readonly Guid _sessionB = Guid.NewGuid();

    private int UserId => _db.ClientUser.Id;

    public Task InitializeAsync() => _db.RunAsync(async db =>
    {
        await db.Users.Where(x => x.Id == UserId).ExecuteUpdateAsync(x => x.SetProperty(u => u.PasswordHash, _hasher.Hash(Password)));
        foreach (var (hash, sessionId) in new[] { ("token-a", _sessionA), ("token-b", _sessionB) })
            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = UserId, TokenHash = hash, SessionId = sessionId, SecurityStamp = _db.ClientUser.SecurityStamp,
                CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(14)
            });
        await db.SaveChangesAsync();
    });

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AdminResetJustBeforeOwnChangeIsWritten_ChangeIsRejectedAndTheResetPasswordWorks()
    {
        var reset = new BeforeFirstUpdate(() => _db.RunAsync(db => Admin(db).ResetPasswordAsync(UserId,
            new ResetPasswordRequest { NewPassword = "Admin12345", ConfirmPassword = "Admin12345" })));
        await using var db = _db.CreateContext(reset);

        var error = await Assert.ThrowsAsync<ValidationException>(() => ChangeAsync(db, _sessionA, "Moja12345"));

        Assert.True(reset.Fired);
        Assert.Equal([WrongCurrentPassword], error.Errors["currentPassword"]);
        var user = await UserAsync();
        Assert.True(_hasher.Verify("Admin12345", user.PasswordHash));
        Assert.Empty(await ActiveTokensAsync());
    }

    [Fact]
    public async Task TwoOwnChangesAtOnce_OneSucceedsAndTheOtherGetsCurrentPasswordError()
    {
        var first = new BeforeFirstUpdate(() => _db.RunAsync(db => ChangeAsync(db, _sessionA, "Prva12345")));
        await using var db = _db.CreateContext(first);

        var error = await Assert.ThrowsAsync<ValidationException>(() => ChangeAsync(db, _sessionB, "Druga12345"));

        Assert.True(first.Fired);
        Assert.Equal([WrongCurrentPassword], error.Errors["currentPassword"]);
        var user = await UserAsync();
        Assert.True(_hasher.Verify("Prva12345", user.PasswordHash));
        // Uređaj čija je promjena uspjela ostaje prijavljen (refresh token sa novim stampom), drugi uređaj je odjavljen.
        var active = Assert.Single(await ActiveTokensAsync());
        Assert.Equal(_sessionA, active.SessionId);
        Assert.Equal(user.SecurityStamp, active.SecurityStamp);
    }

    [Fact]
    public async Task AccountBlockedJustBeforeOwnChangeIsWritten_ChangeIsRejectedAndPasswordStays()
    {
        var block = new BeforeFirstUpdate(() => _db.RunAsync(db => Admin(db).BlockAsync(adminUserId: 0, UserId)));
        await using var db = _db.CreateContext(block);

        var error = await Assert.ThrowsAsync<UnauthorizedException>(() => ChangeAsync(db, _sessionA, "Moja12345"));

        Assert.True(block.Fired);
        Assert.Equal(AuthService.SessionExpired, error.Message);
        var user = await UserAsync();
        Assert.False(user.IsActive);
        Assert.True(_hasher.Verify(Password, user.PasswordHash));
    }

    [Fact]
    public async Task UserDeletedJustBeforeUnblockIsWritten_UserStaysDeletedAndBlocked()
    {
        await _db.RunAsync(db => Admin(db).BlockAsync(adminUserId: 0, UserId));
        var delete = new BeforeFirstUpdate(() => _db.RunAsync(db => Admin(db).DeleteAsync(adminUserId: 0, UserId)));
        await using var db = _db.CreateContext(delete);

        var error = await Assert.ThrowsAsync<NotFoundException>(() => Admin(db).UnblockAsync(UserId));

        Assert.True(delete.Fired);
        Assert.Equal("Korisnik nije pronađen.", error.Message);
        var user = await UserAsync();
        Assert.True(user.IsDeleted);
        Assert.False(user.IsActive);
    }

    [Fact]
    public async Task AdminResetStartedWhileOwnChangeHoldsTheLock_WaitsAndIsAppliedAfterIt()
    {
        Task? reset = null;
        var resetReachedLock = new SignalBeforeFirstUpdate();
        var changing = new BeforeCommit(async () =>
        {
            // Promjena lozinke je upisana, ali još nije potvrđena: reset kreće sada i čeka na zaključanom korisniku.
            reset = Task.Run(async () =>
            {
                await using var adminDb = _db.CreateContext(resetReachedLock);
                await Admin(adminDb).ResetPasswordAsync(UserId, new ResetPasswordRequest { NewPassword = "Admin12345", ConfirmPassword = "Admin12345" });
            });
            await Task.WhenAny(resetReachedLock.Reached.Task, reset);
        });
        await using (var db = _db.CreateContext(changing))
            await ChangeAsync(db, _sessionA, "Moja12345");

        await reset!;
        var user = await UserAsync();
        Assert.True(_hasher.Verify("Admin12345", user.PasswordHash));
        Assert.Empty(await ActiveTokensAsync());
    }

    /// <summary>
    /// Svaka izmjena lozinke, stampa ili statusa naloga u svojoj transakciji prvo zaključa red korisnika (UPDATE bez promjene),
    /// tek onda čita korisnika, i to prije bilo kakvog rada sa refresh tokenima. Refresh tokeni se mijenjaju samo po Id-u:
    /// UPDATE po svim tokenima korisnika bi na SQL Serveru čekao na token koji istovremeni refresh upravo upisuje (a taj upis
    /// čeka na zaključani red korisnika), pa bi nastao deadlock.
    /// </summary>
    [Theory]
    [InlineData("change-password")]
    [InlineData("reset-password")]
    [InlineData("block")]
    [InlineData("unblock")]
    [InlineData("delete")]
    [InlineData("role-change")]
    [InlineData("admin-edit")]
    public async Task EveryAccountChange_LocksTheUserRowFirstAndChangesRefreshTokensOnlyById(string operation)
    {
        var recorder = new TransactionCommandRecorder();
        await using (var db = _db.CreateContext(recorder))
            await Operation(db, operation);

        var commands = recorder.Commands;
        var lockIndex = commands.FindIndex(x => x.Contains("\"Users\"", StringComparison.Ordinal));
        Assert.True(lockIndex >= 0, "Korisnik nije zaključan u transakciji.");
        Assert.StartsWith("UPDATE \"Users\"", commands[lockIndex]);
        Assert.Contains("SET \"SecurityStamp\" = \"u\".\"SecurityStamp\"", commands[lockIndex]);
        Assert.Contains(commands.Skip(lockIndex + 1),
            x => x.StartsWith("SELECT", StringComparison.Ordinal) && x.Contains("FROM \"Users\"", StringComparison.Ordinal));
        var tokensIndex = commands.FindIndex(x => x.Contains("\"RefreshTokens\"", StringComparison.Ordinal));
        Assert.True(tokensIndex < 0 || lockIndex < tokensIndex, "Refresh tokeni su zaključani prije korisnika.");
        Assert.All(commands.Where(x => x.StartsWith("UPDATE \"RefreshTokens\"", StringComparison.Ordinal)), x =>
        {
            Assert.Contains("WHERE \"r\".\"Id\" = ", x);
            Assert.DoesNotContain("\"UserId\"", x);
        });
    }

    /// <summary>
    /// Admin izmjena korisnika zaključava njegov profil prije samog korisnika, istim redoslijedom kao izmjena vlastitog profila
    /// (SaveChanges upisuje profil pa korisnika). Obrnut redoslijed je na SQL Serveru davao deadlock kad korisnik u istom
    /// trenutku snima svoj profil.
    /// </summary>
    [Theory]
    [InlineData("role-change")]
    [InlineData("admin-edit")]
    public async Task AdminUserUpdate_LocksTheProfileBeforeTheUser(string operation)
    {
        var recorder = new TransactionCommandRecorder();
        await using (var db = _db.CreateContext(recorder))
            await Operation(db, operation);

        var profileLock = recorder.Commands.FindIndex(x => x.StartsWith("UPDATE \"ClientProfiles\"", StringComparison.Ordinal));
        var userLock = recorder.Commands.FindIndex(x => x.StartsWith("UPDATE \"Users\"", StringComparison.Ordinal));
        Assert.True(profileLock >= 0, "Profil nije zaključan.");
        Assert.True(profileLock < userLock, "Korisnik je zaključan prije svog profila.");
    }

    /// <summary>
    /// Brisanje korisnika zaključava njegove otvorene pretplate (redom po Id-u) prije samog korisnika, istim redoslijedom kao
    /// prelazi pretplata, koji zaključaju pretplatu pa upisuju obavijest korisniku (strani ključ na njegov red). Korisnik
    /// zaključan prije pretplata bi na SQL Serveru davao deadlock sa istovremenim prelazom iste pretplate.
    /// </summary>
    [Fact]
    public async Task DeleteUser_LocksItsOpenSubscriptionsBeforeTheUser()
    {
        var subscriptionId = await _db.AddSubscriptionAsync(SubscriptionStatus.Active);
        var recorder = new TransactionCommandRecorder();
        await using (var db = _db.CreateContext(recorder))
            await Admin(db).DeleteAsync(adminUserId: 0, UserId);

        var subscriptionLock = recorder.Commands.FindIndex(x => x.StartsWith("UPDATE \"Subscriptions\"", StringComparison.Ordinal));
        var userLock = recorder.Commands.FindIndex(x => x.StartsWith("UPDATE \"Users\"", StringComparison.Ordinal));
        Assert.True(subscriptionLock >= 0, "Otvorena pretplata nije zaključana.");
        Assert.True(userLock >= 0, "Korisnik nije zaključan.");
        Assert.True(subscriptionLock < userLock, "Korisnik je zaključan prije svojih otvorenih pretplata.");
        Assert.Equal(SubscriptionStatus.Cancelled, (await _db.SubscriptionAsync(subscriptionId)).Status);
    }

    private Task Operation(GoBeyondDbContext db, string operation) => operation switch
    {
        "change-password" => ChangeAsync(db, _sessionA, "Moja12345"),
        "reset-password" => Admin(db).ResetPasswordAsync(UserId, new ResetPasswordRequest { NewPassword = "Admin12345", ConfirmPassword = "Admin12345" }),
        "block" => Admin(db).BlockAsync(adminUserId: 0, UserId),
        "unblock" => Admin(db).UnblockAsync(UserId),
        "delete" => Admin(db).DeleteAsync(adminUserId: 0, UserId),
        "role-change" => AdminUpdateAsync(db, UserRole.Admin),
        _ => AdminUpdateAsync(db, UserRole.Client)
    };

    /// <summary>Admin izmjena klijenta: promjena uloge u Admin ili obična izmjena podataka (uloga ostaje Client).</summary>
    private async Task AdminUpdateAsync(GoBeyondDbContext db, UserRole role)
    {
        var client = _db.ClientUser;
        var (genderId, levelId, goalId) = (await db.Genders.Select(x => x.Id).FirstAsync(), await db.FitnessLevels.Select(x => x.Id).FirstAsync(),
            await db.FitnessGoals.Select(x => x.Id).FirstAsync());
        await Admin(db).UpdateUserAsync(adminUserId: 0, UserId, new AdminUpdateUserRequest
        {
            FirstName = client.FirstName, LastName = "Izmijenjena", Username = client.Username, Email = client.Email,
            DateOfBirth = client.DateOfBirth, GenderId = genderId, Role = role,
            Client = role == UserRole.Client
                ? new ClientProfileRequest { WeightKg = 61, HeightCm = 170, FitnessLevelId = levelId, TrainingExperienceYears = 2, FitnessGoalId = goalId }
                : null
        });
    }

    private Task<MessageResponse> ChangeAsync(GoBeyondDbContext db, Guid sessionId, string newPassword) =>
        new AuthService(db, _hasher, new JwtTokenService(Options.Create(Jwt)), new UserAccountValidator(db), null!,
                new RecordingNotificationSender(), Options.Create(Jwt), Options.Create(new UploadOptions()))
            .ChangePasswordAsync(UserId, sessionId,
                new ChangePasswordRequest { CurrentPassword = Password, NewPassword = newPassword, ConfirmPassword = newPassword });

    private AdminUserService Admin(GoBeyondDbContext db) => new(db, new UserAccountValidator(db), _hasher, _db.Workflow(db));

    private Task<User> UserAsync() => _db.RunAsync(db => db.Users.AsNoTracking().SingleAsync(x => x.Id == UserId));

    private Task<List<RefreshToken>> ActiveTokensAsync() =>
        _db.RunAsync(db => db.RefreshTokens.AsNoTracking().Where(x => x.UserId == UserId && x.RevokedAt == null).ToListAsync());

    /// <summary>Jednom, neposredno prije prvog UPDATE-a ovog zahtjeva, izvrši drugi zahtjev do kraja.</summary>
    private sealed class BeforeFirstUpdate(Func<Task> otherRequest) : DbCommandInterceptor
    {
        private int _fired;
        public bool Fired => _fired == 1;

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await FireOnceAsync(command);
            return result;
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            await FireOnceAsync(command);
            return result;
        }

        private async Task FireOnceAsync(DbCommand command)
        {
            if (command.CommandText.StartsWith("UPDATE", StringComparison.Ordinal) && Interlocked.Exchange(ref _fired, 1) == 0)
                await otherRequest();
        }
    }

    /// <summary>Jednom, neposredno prije potvrde transakcije ovog zahtjeva, pokrene drugi zahtjev.</summary>
    private sealed class BeforeCommit(Func<Task> otherRequest) : DbTransactionInterceptor
    {
        private int _fired;

        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _fired, 1) == 0) await otherRequest();
            return result;
        }
    }

    /// <summary>Javlja kad zahtjev stigne do svog prvog UPDATE-a (koji čeka dok druga transakcija drži bazu za upis).</summary>
    private sealed class SignalBeforeFirstUpdate : DbCommandInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("UPDATE", StringComparison.Ordinal)) Reached.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }

    /// <summary>Bilježi naredbe koje se izvršavaju unutar transakcije, redom.</summary>
    private sealed class TransactionCommandRecorder : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Record(command);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Record(command);
            return ValueTask.FromResult(result);
        }

        private void Record(DbCommand command)
        {
            if (command.Transaction is not null) Commands.Add(command.CommandText.TrimStart());
        }
    }
}
