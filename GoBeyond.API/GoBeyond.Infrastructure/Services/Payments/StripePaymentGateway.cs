using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoBeyond.Infrastructure.Services.Payments;

/// <summary>
/// Apstrakcija nad Stripe API-jem (PaymentIntents + Refunds + potpis webhook-a).
/// Svaki POST šalje Idempotency-Key, pa ponovljen zahtjev (npr. nakon prekida mreže) ne kreira
/// drugi PaymentIntent niti drugi povrat.
/// </summary>
public interface IPaymentGateway
{
    bool IsConfigured { get; }
    string PublishableKey { get; }
    string Currency { get; }
    Task<PaymentIntentInfo> CreatePaymentIntentAsync(decimal amount, string receiptEmail, IReadOnlyDictionary<string, string> metadata,
        string idempotencyKey, CancellationToken cancellationToken);
    Task<PaymentIntentInfo> GetPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken);
    Task RefundAsync(string paymentIntentId, string idempotencyKey, CancellationToken cancellationToken);
    bool VerifyWebhookSignature(string payload, string signatureHeader, DateTimeOffset now);
}

/// <summary>
/// Stripe REST klijent (HttpClient, bez dodatnih paketa). Ključevi dolaze isključivo iz
/// konfiguracije (Payments sekcija / environment varijable), nikad iz koda.
/// </summary>
public sealed class StripePaymentGateway(HttpClient http, IOptions<PaymentOptions> options, ILogger<StripePaymentGateway> logger)
    : IPaymentGateway
{
    public const string ChargeAlreadyRefunded = "charge_already_refunded";
    private const int WebhookToleranceSeconds = 300;
    private PaymentOptions Settings => options.Value;

    public bool IsConfigured => Settings.IsConfigured;
    public string PublishableKey => Settings.PublishableKey;
    public string Currency => Settings.Currency.ToLowerInvariant();

    public async Task<PaymentIntentInfo> CreatePaymentIntentAsync(decimal amount, string receiptEmail,
        IReadOnlyDictionary<string, string> metadata, string idempotencyKey, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["amount"] = ToMinorUnits(amount).ToString(CultureInfo.InvariantCulture),
            ["currency"] = Currency,
            ["payment_method_types[]"] = "card",
            ["receipt_email"] = receiptEmail
        };
        foreach (var (key, value) in metadata) form[$"metadata[{key}]"] = value;

        using var request = CreateRequest(HttpMethod.Post, "payment_intents", idempotencyKey);
        request.Content = new FormUrlEncodedContent(form);
        return ParseIntent(await SendAsync(request, cancellationToken));
    }

    public async Task<PaymentIntentInfo> GetPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"payment_intents/{Uri.EscapeDataString(paymentIntentId)}", idempotencyKey: null);
        return ParseIntent(await SendAsync(request, cancellationToken));
    }

    /// <summary>
    /// Puni povrat PaymentIntent-a. Ako je naplata na Stripe-u već potpuno vraćena (npr. ranijim pokušajem čiji je
    /// Idempotency-Key istekao nakon 24 h, ili ručno iz Stripe Dashboard-a), Stripe vraća "charge_already_refunded" -
    /// novac je već kod klijenta, pa se povrat smatra izvršenim (inače bi RefundPending i odbijanje zahtjeva zauvijek padali).
    /// </summary>
    public async Task RefundAsync(string paymentIntentId, string idempotencyKey, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "refunds", idempotencyKey);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["payment_intent"] = paymentIntentId });
        using var _ = await SendAsync(request, cancellationToken, toleratedErrorCode: ChargeAlreadyRefunded);
    }

    /// <summary>Stripe-Signature: "t=timestamp,v1=hex(HMAC-SHA256(secret, "{t}.{payload}"))".</summary>
    public bool VerifyWebhookSignature(string payload, string signatureHeader, DateTimeOffset now)
    {
        var secret = Settings.WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signatureHeader)) return false;

        var parts = signatureHeader.Split(',', StringSplitOptions.TrimEntries)
            .Select(x => x.Split('=', 2))
            .Where(x => x.Length == 2)
            .ToList();
        var timestampText = parts.FirstOrDefault(x => x[0] == "t")?[1];
        if (!long.TryParse(timestampText, out var timestamp) ||
            Math.Abs(now.ToUnixTimeSeconds() - timestamp) > WebhookToleranceSeconds)
            return false;

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestampText}.{payload}"));
        foreach (var signature in parts.Where(x => x[0] == "v1").Select(x => x[1]))
        {
            try
            {
                if (CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature))) return true;
            }
            catch (FormatException)
            {
                // neispravan hex potpis - provjeri sljedeći
            }
        }
        return false;
    }

    public static long ToMinorUnits(decimal amount) =>
        checked((long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string? idempotencyKey)
    {
        if (!IsConfigured)
            throw new ValidationException("Stripe plaćanje nije konfigurisano na serveru.");

        var request = new HttpRequestMessage(method, new Uri(new Uri(Settings.ApiBaseUrl), path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings.SecretKey);
        if (method == HttpMethod.Post)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new ArgumentException("Svaki POST prema Stripe-u mora imati Idempotency-Key.", nameof(idempotencyKey));
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }
        return request;
    }

    /// <param name="toleratedErrorCode">Stripe error.code koji znači da je traženo stanje već postignuto (odgovor se tada vraća bez greške).</param>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken,
        string? toleratedErrorCode = null)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Stripe request failed.");
            throw new ValidationException("Komunikacija sa Stripe servisom nije uspjela. Pokušajte ponovo.");
        }

        if (response.IsSuccessStatusCode) return response;

        var (errorType, errorCode) = await ReadErrorAsync(response, cancellationToken);
        var path = request.RequestUri?.AbsolutePath;
        if (toleratedErrorCode is not null && errorCode == toleratedErrorCode)
        {
            logger.LogInformation("Stripe answered {ErrorCode} for {Path}; the requested state already exists.", errorCode, path);
            return response;
        }

        // Tijelo greške se ne prosljeđuje klijentu (može sadržavati detalje zahtjeva); loguju se samo tip i kod greške.
        logger.LogWarning("Stripe returned {StatusCode} ({ErrorType}/{ErrorCode}) for {Path}.",
            (int)response.StatusCode, errorType, errorCode, path);
        response.Dispose();
        throw new ValidationException("Stripe nije prihvatio zahtjev. Provjerite Stripe test ključeve i pokušajte ponovo.");
    }

    /// <summary>Stripe greška: { "error": { "type": "...", "code": "...", "message": "..." } }.</summary>
    private static async Task<(string? Type, string? Code)> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!json.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object) return (null, null);
            return (Text(error, "type"), Text(error, "code"));
        }
        catch (JsonException)
        {
            return (null, null);
        }

        static string? Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static PaymentIntentInfo ParseIntent(HttpResponseMessage response)
    {
        using (response)
        {
            using var json = JsonDocument.Parse(response.Content.ReadAsStream());
            return PaymentIntentInfo.FromJson(json.RootElement);
        }
    }
}
