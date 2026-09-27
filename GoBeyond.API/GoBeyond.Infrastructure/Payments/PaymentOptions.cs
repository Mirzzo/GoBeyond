namespace GoBeyond.Infrastructure.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";
    public string Mode { get; set; } = "Demo";
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string Currency { get; set; } = "bam";
    public string ApiBaseUrl { get; set; } = "https://api.stripe.com/v1/";
    public bool IsDemo => Mode.Equals("Demo", StringComparison.OrdinalIgnoreCase);
}
