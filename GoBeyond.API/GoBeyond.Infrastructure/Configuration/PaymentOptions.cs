namespace GoBeyond.Infrastructure.Configuration;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Stripe je konfigurisan tek kad su postavljeni i tajni i javni (test) ključ.</summary>
    public bool IsConfigured =>
        SecretKey.StartsWith("sk_", StringComparison.Ordinal) &&
        PublishableKey.StartsWith("pk_", StringComparison.Ordinal);
}
