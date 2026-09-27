namespace GoBeyond.Infrastructure.Configuration;

// Sve vrijednosti dolaze iz appsettings.Shared.json (jedno mjesto), a mogu se
// pregaziti environment varijablama (npr. Payments__SecretKey u .env za docker-compose).

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; }
    public int RefreshTokenLifetimeDays { get; set; }
}
