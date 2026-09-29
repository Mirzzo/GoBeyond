using System.Security.Claims;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Security;

namespace GoBeyond.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(JwtTokenService.UserIdClaim), out var id)
            ? id
            : throw new UnauthorizedAccessException();

    public static int? TryGetUserId(this ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && int.TryParse(user.FindFirstValue(JwtTokenService.UserIdClaim), out var id)
            ? id
            : null;

    /// <summary>Sesija (RefreshToken.SessionId) uz koju je access token izdat; null ako token nema tvrdnju "sid".</summary>
    public static Guid? GetSessionId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(JwtTokenService.SessionIdClaim), out var id) ? id : null;

    public static UserRole GetRole(this ClaimsPrincipal user) =>
        Enum.TryParse<UserRole>(user.FindFirstValue(JwtTokenService.RoleClaim), out var role)
            ? role
            : throw new UnauthorizedAccessException();
}
