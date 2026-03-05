namespace GoBeyond.Infrastructure.Utilities;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = "ReplaceWithLongDevSecretKeyAtLeast32Chars";
    public string Issuer { get; set; } = "GoBeyond";
    public string Audience { get; set; } = "GoBeyondClients";
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}
