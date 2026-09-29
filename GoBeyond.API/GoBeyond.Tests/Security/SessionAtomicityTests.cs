using GoBeyond.Core.DTOs.Auth;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
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
/// Kraj sesija (novi stamp, opoziv refresh tokena) se snima u istoj transakciji kao izmjena korisnika: ako snimanje korisnika
/// ne uspije, ne ostaju opozvani tokeni uz staru lozinku i aktivan nalog. Vlastita promjena lozinke zadržava samo sesiju
/// uređaja koji ju je promijenio (njen refresh token dobija novi stamp).
/// </summary>
public sealed class SessionAtomicityTests : IDisposable
{
    private const string Password = "Lozinka123";

    private static readonly JwtOptions Jwt = new()
    {
        SecretKey = "GoBeyond_Test_Secret_Key_For_Session_Atomicity_2026", Issuer = "GoBeyond", Audience = "GoBeyondClients",
        AccessTokenLifetimeMinutes = 60, RefreshTokenLifetimeDays = 14
    };

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Guid _callerSessionId = Guid.NewGuid();
    private readonly int _userId;
    private readonly Guid _stamp;

    public SessionAtomicityTests()
    {
        _connection.Open();
        using var db = SqliteTestDbContext.Create(_connection);
        db.Database.EnsureCreated();
        var user = new User
        {
            FirstName = "Atomska", LastName = "Sesija", Username = "atomic", Email = "atomic@test.ba", DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = new Gender { Name = "Muško" }, PasswordHash = new PasswordHasher().Hash(Password), Role = UserRole.Client
        };
        foreach (var (hash, sessionId) in new[] { ("token-caller", _callerSessionId), ("token-other", Guid.NewGuid()) })
            user.RefreshTokens.Add(new RefreshToken
            {
                TokenHash = hash, SessionId = sessionId, SecurityStamp = user.SecurityStamp, ExpiresAt = DateTime.UtcNow.AddDays(14)
            });
        db.Users.Add(user);
        db.SaveChanges();
        (_userId, _stamp) = (user.Id, user.SecurityStamp);
    }

    public void Dispose() => _connection.Dispose();

    [Theory]
    [InlineData("reset-password")]
    [InlineData("block")]
    [InlineData("change-password")]
    public async Task SaveSucceeds_EndsSessionsExceptTheCallersOnPasswordChange(string operation)
    {
        await using (var db = SqliteTestDbContext.Create(_connection))
            await Operation(db, operation)();

        await using var check = SqliteTestDbContext.Create(_connection);
        var stamp = await check.Users.Where(x => x.Id == _userId).Select(x => x.SecurityStamp).SingleAsync();
        Assert.NotEqual(_stamp, stamp);
        var active = await check.RefreshTokens.AsNoTracking().Where(x => x.UserId == _userId && x.RevokedAt == null).ToListAsync();
        if (operation == "change-password")
        {
            var caller = Assert.Single(active);
            Assert.Equal(_callerSessionId, caller.SessionId);
            Assert.Equal(stamp, caller.SecurityStamp);
        }
        else
        {
            Assert.Empty(active);
        }
    }

    [Theory]
    [InlineData("reset-password")]
    [InlineData("block")]
    [InlineData("change-password")]
    public async Task SaveFailsAfterSessionsEnded_NothingIsRevoked(string operation)
    {
        await using (var db = new SqliteTestDbContext(new DbContextOptionsBuilder<GoBeyondDbContext>()
                         .UseSqlite(_connection).AddInterceptors(new FailingSave()).Options))
            await Assert.ThrowsAsync<DbUpdateException>(Operation(db, operation));

        await using var check = SqliteTestDbContext.Create(_connection);
        var user = await check.Users.AsNoTracking().SingleAsync(x => x.Id == _userId);
        Assert.Equal(_stamp, user.SecurityStamp);
        Assert.True(user.IsActive);
        Assert.True(new PasswordHasher().Verify(Password, user.PasswordHash));
        Assert.Equal(2, await check.RefreshTokens.CountAsync(x => x.UserId == _userId && x.RevokedAt == null && x.SecurityStamp == _stamp));
    }

    /// <summary>Admin reset lozinke, blokiranje ili vlastita promjena lozinke (uređaj sa sesijom <c>_callerSessionId</c>).</summary>
    private Func<Task> Operation(GoBeyondDbContext db, string operation)
    {
        var admin = new AdminUserService(db, new UserAccountValidator(db), new PasswordHasher(), null!);
        var auth = new AuthService(db, new PasswordHasher(), new JwtTokenService(Options.Create(Jwt)), new UserAccountValidator(db),
            null!, new RecordingNotificationSender(), Options.Create(Jwt), Options.Create(new UploadOptions()));
        return operation switch
        {
            "reset-password" => () => admin.ResetPasswordAsync(_userId, new ResetPasswordRequest { NewPassword = "Novi12345", ConfirmPassword = "Novi12345" }),
            "block" => () => admin.BlockAsync(adminUserId: 0, _userId),
            _ => () => auth.ChangePasswordAsync(_userId, _callerSessionId,
                new ChangePasswordRequest { CurrentPassword = Password, NewPassword = "Nova12345", ConfirmPassword = "Nova12345" })
        };
    }

    /// <summary>Snimanje izmjena korisnika ne uspije (npr. prekid veze ili greška baze).</summary>
    private sealed class FailingSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Simulirana greška pri snimanju.");
    }
}
