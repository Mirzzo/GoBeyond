using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GoBeyond.Core.Entities;
using GoBeyond.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GoBeyond.Infrastructure.Security;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(User user, Guid sessionId);
    string CreateRefreshToken();
    string HashRefreshToken(string refreshToken);
}

public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    public const string RoleClaim = "role";
    public const string UserIdClaim = JwtRegisteredClaimNames.Sub;

    /// <summary>User.SecurityStamp u trenutku izdavanja; token bez ove tvrdnje ili sa starom vrijednošću nije važeći.</summary>
    public const string SecurityStampClaim = "stamp";

    /// <summary>RefreshToken.SessionId sesije uz koju je token izdat (promjena lozinke zadržava samo tu sesiju).</summary>
    public const string SessionIdClaim = JwtRegisteredClaimNames.Sid;

    public (string Token, DateTime ExpiresAt) CreateAccessToken(User user, Guid sessionId)
    {
        var settings = options.Value;
        var expiresAt = DateTime.UtcNow.AddMinutes(settings.AccessTokenLifetimeMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)), SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(UserIdClaim, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(RoleClaim, user.Role.ToString()),
            new(SecurityStampClaim, user.SecurityStamp.ToString("N")),
            new(SessionIdClaim, sessionId.ToString("N")),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            expires: expiresAt, signingCredentials: credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string CreateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    /// <summary>U bazi se čuva samo SHA-256 hash refresh tokena.</summary>
    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
