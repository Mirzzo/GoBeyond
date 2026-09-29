using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using GoBeyond.API.Validation;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Security;
using GoBeyond.Infrastructure.Services.Auth;
using GoBeyond.Infrastructure.Services.Mentors;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GoBeyond.Tests.Security;

/// <summary>
/// Access token prestaje važiti odmah nakon admin reseta lozinke, blokiranja, promjene uloge i vlastite promjene lozinke
/// (security stamp u tokenu), i ne oživljava nakon odblokiranja ili vraćanja uloge. Vlastita promjena lozinke zadržava samo
/// sesiju uređaja koji ju je promijenio (refresh radi). Plus 401/403 tekstovi iz ugovora.
/// </summary>
public sealed class SessionInvalidationTests(GoBeyondApiFactory factory) : IClassFixture<GoBeyondApiFactory>
{
    private const string Me = "/api/user-profile/me";

    [Fact]
    public async Task AdminResetPassword_InvalidatesExistingAccessToken()
    {
        var session = await SecurityTestApi.RegisterClientAsync(factory, "sess_reset");
        var user = SecurityTestApi.WithToken(factory, session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync(Me)).StatusCode);

        var reset = await factory.ClientFor(TestUsers.Admin).PutAsJsonAsync($"/api/admin/users/{session.User.Id}/reset-password",
            new { newPassword = "Novi12345", confirmPassword = "Novi12345" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var response = await user.GetAsync(Me);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorMessages.SessionInvalid, (await SecurityTestApi.ErrorAsync(response)).Message);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SecurityTestApi.RefreshAsync(factory, session.RefreshToken)).StatusCode);

        var fresh = await SecurityTestApi.LoginAsync(factory, "sess_reset", "Novi12345");
        Assert.Equal(HttpStatusCode.OK, (await SecurityTestApi.WithToken(factory, fresh.AccessToken).GetAsync(Me)).StatusCode);
    }

    [Fact]
    public async Task Unblock_DoesNotReviveAccessTokenIssuedBeforeBlock()
    {
        var session = await SecurityTestApi.RegisterClientAsync(factory, "sess_block");
        var user = SecurityTestApi.WithToken(factory, session.AccessToken);
        var admin = factory.ClientFor(TestUsers.Admin);

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync($"/api/admin/users/{session.User.Id}/block", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync(Me)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync($"/api/admin/users/{session.User.Id}/unblock", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync(Me)).StatusCode);
        var fresh = await SecurityTestApi.LoginAsync(factory, "sess_block");
        Assert.Equal(HttpStatusCode.OK, (await SecurityTestApi.WithToken(factory, fresh.AccessToken).GetAsync(Me)).StatusCode);
    }

    [Fact]
    public async Task RoleChangedAndReverted_DoesNotReviveOldAccessToken()
    {
        var session = await SecurityTestApi.RegisterClientAsync(factory, "sess_role");
        var user = SecurityTestApi.WithToken(factory, session.AccessToken);
        var admin = factory.ClientFor(TestUsers.Admin);
        var url = $"/api/admin/users/{session.User.Id}";

        var toMentor = await admin.PutAsJsonAsync(url, SecurityTestApi.AdminUpdateBody(factory, "sess_role", "Mentor", withMentor: true));
        Assert.Equal(HttpStatusCode.OK, toMentor.StatusCode);
        var backToClient = await admin.PutAsJsonAsync(url, SecurityTestApi.AdminUpdateBody(factory, "sess_role", "Client"));
        Assert.Equal(HttpStatusCode.OK, backToClient.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync(Me)).StatusCode);
    }

    [Fact]
    public async Task AdminDelete_InvalidatesAccessAndRefreshToken()
    {
        var session = await SecurityTestApi.RegisterClientAsync(factory, "sess_delete");

        Assert.Equal(HttpStatusCode.NoContent, (await factory.ClientFor(TestUsers.Admin).DeleteAsync($"/api/admin/users/{session.User.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SecurityTestApi.WithToken(factory, session.AccessToken).GetAsync(Me)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SecurityTestApi.RefreshAsync(factory, session.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task OwnChangePassword_KeepsCallersSessionAndEndsOtherSessions()
    {
        var registered = await SecurityTestApi.RegisterClientAsync(factory, "sess_chpwd");
        var other = await SecurityTestApi.LoginAsync(factory, "sess_chpwd");
        // Uređaj koji mijenja lozinku je već jednom obnovio tokene: sesija (sid) ostaje ista kroz rotacije.
        var caller = await RefreshedAsync(registered.RefreshToken);

        var change = await SecurityTestApi.WithToken(factory, caller.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = SecurityTestApi.Password, newPassword = "Nova12345", confirmPassword = "Nova12345" });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SecurityTestApi.WithToken(factory, other.AccessToken).GetAsync(Me)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SecurityTestApi.RefreshAsync(factory, other.RefreshToken)).StatusCode);

        // Aplikacija na 401 (stari stamp u access tokenu) obnovi tokene refresh tokenom ovog uređaja i nastavi bez prijave.
        var stale = await SecurityTestApi.WithToken(factory, caller.AccessToken).GetAsync(Me);
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        var renewed = await RefreshedAsync(caller.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, (await SecurityTestApi.WithToken(factory, renewed.AccessToken).GetAsync(Me)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SecurityTestApi.RefreshAsync(factory, renewed.RefreshToken)).StatusCode);

        var oldPassword = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { username = "sess_chpwd", password = SecurityTestApi.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        var fresh = await SecurityTestApi.LoginAsync(factory, "sess_chpwd", "Nova12345");
        Assert.Equal(HttpStatusCode.OK, (await SecurityTestApi.WithToken(factory, fresh.AccessToken).GetAsync(Me)).StatusCode);
    }

    [Fact]
    public async Task OwnChangePasswordWithWrongCurrentPassword_KeepsAllSessions()
    {
        var first = await SecurityTestApi.RegisterClientAsync(factory, "sess_chpwd_wrong");
        var second = await SecurityTestApi.LoginAsync(factory, "sess_chpwd_wrong");

        var change = await SecurityTestApi.WithToken(factory, first.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "Pogresna123", newPassword = "Nova12345", confirmPassword = "Nova12345" });
        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await SecurityTestApi.WithToken(factory, first.AccessToken).GetAsync(Me)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SecurityTestApi.WithToken(factory, second.AccessToken).GetAsync(Me)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SecurityTestApi.RefreshAsync(factory, second.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task RefreshedSession_KeepsWorkingAfterOrdinaryRefresh()
    {
        var session = await SecurityTestApi.RegisterClientAsync(factory, "sess_refresh");

        var refresh = await SecurityTestApi.RefreshAsync(factory, session.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = (await refresh.Content.ReadFromJsonAsync<AuthBody>(SecurityTestApi.Json))!;

        Assert.Equal(HttpStatusCode.OK, (await SecurityTestApi.WithToken(factory, rotated.AccessToken).GetAsync(Me)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SecurityTestApi.WithToken(factory, session.AccessToken).GetAsync(Me)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SecurityTestApi.RefreshAsync(factory, session.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task TokenWithoutSecurityStamp_IsRejected()
    {
        var session = await SecurityTestApi.RegisterClientAsync(factory, "sess_nostamp");
        var token = SignToken(
            [new(JwtTokenService.UserIdClaim, session.User.Id.ToString()), new(JwtTokenService.RoleClaim, "Client")],
            DateTime.UtcNow.AddMinutes(30));

        var response = await SecurityTestApi.WithToken(factory, token).GetAsync(Me);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorMessages.SessionInvalid, (await SecurityTestApi.ErrorAsync(response)).Message);
    }

    [Fact]
    public async Task ExpiredToken_Returns401WithContractText()
    {
        // Istek se provjerava prije svega ostalog (potpis je ispravan), pa je poruka ona za istekao token.
        var session = await SecurityTestApi.RegisterClientAsync(factory, "sess_expired");
        var token = SignToken(
            [new(JwtTokenService.UserIdClaim, session.User.Id.ToString()), new(JwtTokenService.RoleClaim, "Client")],
            DateTime.UtcNow.AddHours(-1));

        var response = await SecurityTestApi.WithToken(factory, token).GetAsync(Me);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorMessages.Unauthorized, (await SecurityTestApi.ErrorAsync(response)).Message);
    }

    [Fact]
    public async Task NoToken_Returns401WithContractText()
    {
        var response = await factory.CreateClient().GetAsync(Me);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorMessages.Unauthorized, (await SecurityTestApi.ErrorAsync(response)).Message);
    }

    [Fact]
    public async Task ClientRequestingCertificate_Gets403WithCertificateText_EvenForUnknownId()
    {
        var certificateId = factory.Query(db => db.MentorCertificates.AsNoTracking().Select(x => x.Id).First());
        var client = factory.ClientFor(TestUsers.Client);

        foreach (var id in new[] { certificateId, 999999 })
        {
            var response = await client.GetAsync($"/api/certificates/{id}/file");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(CertificateFileService.Forbidden, (await SecurityTestApi.ErrorAsync(response)).Message);
        }
    }

    [Fact]
    public async Task LoginAfterBlockedLogin_StillReturnsBlockedMessage()
    {
        var session = await SecurityTestApi.RegisterClientAsync(factory, "sess_blocked_login");
        await factory.ClientFor(TestUsers.Admin).PutAsync($"/api/admin/users/{session.User.Id}/block", null);

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { username = "sess_blocked_login", password = SecurityTestApi.Password });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AuthService.BlockedMessage, (await SecurityTestApi.ErrorAsync(response)).Message);
    }

    private async Task<AuthBody> RefreshedAsync(string refreshToken)
    {
        var response = await SecurityTestApi.RefreshAsync(factory, refreshToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthBody>(SecurityTestApi.Json))!;
    }

    private string SignToken(List<Claim> claims, DateTime expiresAt)
    {
        using var scope = factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims,
            notBefore: expiresAt.AddHours(-2), expires: expiresAt, signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
