using System.Text;
using GoBeyond.API.Validation;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GoBeyond.API.Extensions;

public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string MentorOnly = nameof(MentorOnly);
    public const string ClientOnly = nameof(ClientOnly);
    public const string MentorOrAdmin = nameof(MentorOrAdmin);
}

public static class AuthenticationExtensions
{
    /// <summary>
    /// JWT Bearer autentifikacija + politike po ulogama. U Program.cs se svi kontroleri mapiraju sa
    /// RequireAuthorization(), pa je prijava obavezna za SVAKI endpoint koji nije eksplicitno označen
    /// sa [AllowAnonymous] (nema slučajno nezaštićenih ruta).
    /// </summary>
    public static IServiceCollection AddGoBeyondAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtTokenService.UserIdClaim,
                    RoleClaimType = JwtTokenService.RoleClaim
                };
                options.Events = new JwtBearerEvents
                {
                    // Token je validan samo dok je korisnik aktivan, neobrisan i ima istu ulogu kao pri izdavanju.
                    OnTokenValidated = async context =>
                    {
                        var idText = context.Principal?.FindFirst(JwtTokenService.UserIdClaim)?.Value;
                        var role = context.Principal?.FindFirst(JwtTokenService.RoleClaim)?.Value;
                        if (!int.TryParse(idText, out var userId))
                        {
                            context.Fail(ErrorMessages.SessionInvalid);
                            return;
                        }

                        var db = context.HttpContext.RequestServices.GetRequiredService<GoBeyondDbContext>();
                        var user = await db.Users.AsNoTracking()
                            .Where(x => x.Id == userId)
                            .Select(x => new { x.IsActive, x.IsDeleted, x.Role })
                            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);
                        if (user is null || !user.IsActive || user.IsDeleted || user.Role.ToString() != role)
                            context.Fail(ErrorMessages.SessionInvalid);
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new ErrorResponse(
                            context.AuthenticateFailure is null ? ErrorMessages.Unauthorized : ErrorMessages.SessionInvalid));
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new ErrorResponse(ErrorMessages.Forbidden));
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole("Admin"))
            .AddPolicy(Policies.MentorOnly, policy => policy.RequireRole("Mentor"))
            .AddPolicy(Policies.ClientOnly, policy => policy.RequireRole("Client"))
            .AddPolicy(Policies.MentorOrAdmin, policy => policy.RequireRole("Mentor", "Admin"));

        return services;
    }
}
