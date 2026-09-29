using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using GoBeyond.Core.DTOs.Auth;
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
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Security;

/// <summary>
/// Rotacija refresh tokena mora biti atomska: kad dva zahtjeva istovremeno iskoriste isti token, samo jedan dobija nove tokene.
/// Refresh koji se preklopi sa resetom ili promjenom lozinke ne smije izdati tokene koji prežive kraj sesija.
/// Utrke se reprodukuju deterministički: nakon određene naredbe prvog zahtjeva, a prije njegove sljedeće naredbe ili
/// transakcije, drugi zahtjev na drugoj konekciji se kompletno izvrši.
/// </summary>
public sealed class RefreshTokenRaceTests : IDisposable
{
    private const string RawToken = "race-refresh-token";
    private const string Password = "Lozinka123";

    private static readonly JwtOptions Jwt = new()
    {
        SecretKey = "GoBeyond_Test_Secret_Key_For_Refresh_Race_2026", Issuer = "GoBeyond", Audience = "GoBeyondClients",
        AccessTokenLifetimeMinutes = 60, RefreshTokenLifetimeDays = 14
    };

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"gobeyond-refresh-race-{Guid.NewGuid():N}.db");
    private readonly JwtTokenService _tokens = new(Options.Create(Jwt));
    private readonly int _userId;

    public RefreshTokenRaceTests()
    {
        using var db = CreateContext();
        db.Database.EnsureCreated();
        var user = new User
        {
            FirstName = "Utrka", LastName = "Test", Username = "race", Email = "race@test.ba", DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = new Gender { Name = "Muško" }, PasswordHash = new PasswordHasher().Hash(Password), Role = UserRole.Client
        };
        db.Users.Add(user);
        db.RefreshTokens.Add(new RefreshToken
        {
            User = user, TokenHash = _tokens.HashRefreshToken(RawToken), SessionId = Guid.NewGuid(), SecurityStamp = user.SecurityStamp,
            CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(14)
        });
        db.SaveChanges();
        _userId = user.Id;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public async Task ConcurrentRefreshWithSameToken_OnlyOneRequestGetsNewTokens()
    {
        Exception? secondError = null;
        AuthResponse? secondResult = null;
        var interceptor = new InterleaveAfter(IsTokenRead, async () =>
        {
            await using var otherDb = CreateContext();
            try { secondResult = await CreateService(otherDb).RefreshAsync(RawToken); }
            catch (Exception ex) { secondError = ex; }
        });

        await using var db = CreateContext(interceptor);
        Exception? firstError = null;
        AuthResponse? firstResult = null;
        try { firstResult = await CreateService(db).RefreshAsync(RawToken); }
        catch (Exception ex) { firstError = ex; }

        Assert.True(interceptor.Fired, "Drugi zahtjev nije pokrenut između čitanja i upisa prvog zahtjeva.");
        Assert.Equal(1, new[] { firstResult, secondResult }.Count(x => x is not null));
        var failure = Assert.IsType<UnauthorizedException>(firstError ?? secondError);
        Assert.Equal(AuthService.SessionExpired, failure.Message);

        await using var check = CreateContext();
        Assert.Equal(1, await check.RefreshTokens.CountAsync(x => x.RevokedAt == null));
    }

    [Fact]
    public async Task RotatedToken_CannotBeUsedAgain()
    {
        await using (var db = CreateContext())
            await CreateService(db).RefreshAsync(RawToken);

        await using var again = CreateContext();
        var error = await Assert.ThrowsAsync<UnauthorizedException>(() => CreateService(again).RefreshAsync(RawToken));
        Assert.Equal(AuthService.SessionExpired, error.Message);
    }

    [Fact]
    public async Task AdminResetDuringRefresh_TokensIssuedByThatRefreshAreRejected() =>
        await AssertRefreshLosesToEndedSessions(db => new AdminUserService(db, new UserAccountValidator(db), new PasswordHasher(), null!)
            .ResetPasswordAsync(_userId, new ResetPasswordRequest { NewPassword = "Novi12345", ConfirmPassword = "Novi12345" }));

    [Fact]
    public async Task PasswordChangeOnOtherDeviceDuringRefresh_TokensIssuedByThatRefreshAreRejected() =>
        await AssertRefreshLosesToEndedSessions(db => CreateService(db).ChangePasswordAsync(_userId, sessionId: Guid.NewGuid(),
            new ChangePasswordRequest { CurrentPassword = Password, NewPassword = "Nova12345", ConfirmPassword = "Nova12345" }));

    /// <summary>
    /// Refresh je već opozvao stari token, a prije upisa novog se završe sve sesije korisnika. Novi refresh token se ipak upiše
    /// (opoziv ga nije mogao obuhvatiti), ali nosi stari stamp: i on i novi access token odmah ne važe.
    /// </summary>
    private async Task AssertRefreshLosesToEndedSessions(Func<GoBeyondDbContext, Task> endSessions)
    {
        var interceptor = new InterleaveAfter(IsTokenRevoke, async () =>
        {
            await using var otherDb = CreateContext();
            await endSessions(otherDb);
        });

        AuthResponse issued;
        await using (var db = CreateContext(interceptor))
            issued = await CreateService(db).RefreshAsync(RawToken);

        Assert.True(interceptor.Fired, "Kraj sesija nije izvršen između opoziva starog i upisa novog refresh tokena.");
        await using var check = CreateContext();
        var stamp = await check.Users.Where(x => x.Id == _userId).Select(x => x.SecurityStamp).SingleAsync();
        var accessStamp = new JwtSecurityTokenHandler().ReadJwtToken(issued.AccessToken).Claims
            .Single(x => x.Type == JwtTokenService.SecurityStampClaim).Value;
        Assert.NotEqual(stamp.ToString("N"), accessStamp);

        var error = await Assert.ThrowsAsync<UnauthorizedException>(() => CreateService(check).RefreshAsync(issued.RefreshToken));
        Assert.Equal(AuthService.SessionExpired, error.Message);
    }

    private static bool IsTokenRead(DbCommand command) => command.CommandText.Contains("FROM \"RefreshTokens\"", StringComparison.Ordinal);

    private static bool IsTokenRevoke(DbCommand command) => command.CommandText.Contains("UPDATE \"RefreshTokens\"", StringComparison.Ordinal);

    private GoBeyondDbContext CreateContext(params IInterceptor[] interceptors) =>
        new SqliteTestDbContext(new DbContextOptionsBuilder<GoBeyondDbContext>()
            .UseSqlite($"Data Source={_path}").AddInterceptors(interceptors).Options);

    private AuthService CreateService(GoBeyondDbContext db) => new(db, new PasswordHasher(), _tokens, new UserAccountValidator(db),
        null!, new RecordingNotificationSender(), Options.Create(Jwt), Options.Create(new UploadOptions()));

    /// <summary>Nakon naredbe koja zadovoljava <c>trigger</c>, prije prve sljedeće naredbe/transakcije, jednom izvrši drugi zahtjev.</summary>
    private sealed class InterleaveAfter(Func<DbCommand, bool> trigger, Func<Task> otherRequest) : DbCommandInterceptor, IDbTransactionInterceptor
    {
        private bool _triggered;
        public bool Fired { get; private set; }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!Fired && trigger(command)) _triggered = true;
            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (!Fired && trigger(command)) _triggered = true;
            return ValueTask.FromResult(result);
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            await FireOnceAsync();
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await FireOnceAsync();
            return result;
        }

        public async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection, TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        {
            await FireOnceAsync();
            return result;
        }

        private async Task FireOnceAsync()
        {
            if (!_triggered || Fired) return;
            Fired = true;
            await otherRequest();
        }
    }
}
