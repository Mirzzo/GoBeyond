using System.Text.Json;

namespace GoBeyond.Infrastructure.Services.Payments;

/// <summary>Podaci Stripe PaymentIntent-a koje backend koristi (iz API odgovora ili iz webhook događaja).</summary>
public sealed record PaymentIntentInfo(
    string Id,
    string ClientSecret,
    string Status,
    long AmountMinor,
    string Currency,
    IReadOnlyDictionary<string, string> Metadata)
{
    public const string MetadataSubscriptionId = "subscriptionId";
    public const string MetadataPurpose = "purpose";

    /// <summary>Parsira Stripe PaymentIntent JSON objekat (isti oblik u odgovoru API-ja i u data.object webhook-a).</summary>
    public static PaymentIntentInfo FromJson(JsonElement json)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (json.TryGetProperty("metadata", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in meta.EnumerateObject())
                metadata[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.ToString();
        }

        return new PaymentIntentInfo(
            GetString(json, "id"),
            GetString(json, "client_secret"),
            GetString(json, "status"),
            json.TryGetProperty("amount", out var amount) && amount.ValueKind == JsonValueKind.Number ? amount.GetInt64() : 0,
            GetString(json, "currency"),
            metadata);
    }

    private static string GetString(JsonElement json, string name) =>
        json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}
