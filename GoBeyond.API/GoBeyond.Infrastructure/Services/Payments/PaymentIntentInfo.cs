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

    /// <summary>
    /// Vrijeme naplate (latest_charge.created) kad je Stripe vratio proširen latest_charge (GET PaymentIntent-a); u webhook-u
    /// je latest_charge samo id, pa je null.
    /// </summary>
    public DateTime? ChargedAt { get; init; }

    /// <summary>
    /// Parsira Stripe PaymentIntent JSON objekat (isti oblik u odgovoru API-ja i u data.object webhook-a). Element koji nije
    /// objekat daje prazan PaymentIntent (bez id-a), pa ga pozivalac ignoriše.
    /// </summary>
    public static PaymentIntentInfo FromJson(JsonElement json)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (json.ValueKind != JsonValueKind.Object)
            return new PaymentIntentInfo(string.Empty, string.Empty, string.Empty, 0, string.Empty, metadata);

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
            json.TryGetProperty("amount", out var amount) && amount.ValueKind == JsonValueKind.Number && amount.TryGetInt64(out var minor) ? minor : 0,
            GetString(json, "currency"),
            metadata)
        {
            ChargedAt = json.TryGetProperty("latest_charge", out var charge) && charge.ValueKind == JsonValueKind.Object &&
                        charge.TryGetProperty("created", out var created) && created.ValueKind == JsonValueKind.Number &&
                        created.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
                : null
        };
    }

    private static string GetString(JsonElement json, string name) =>
        json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}
