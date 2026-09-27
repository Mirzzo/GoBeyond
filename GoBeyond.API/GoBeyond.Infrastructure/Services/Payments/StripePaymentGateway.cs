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

    public async Task RefundAsync(string paymentIntentId, string idempotencyKey, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "refunds", idempotencyKey);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["payment_intent"] = paymentIntentId });
        using var _ = await SendAsync(request, cancellationToken);
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

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
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

        if (!response.IsSuccessStatusCode)
        {
            // Tijelo greške se ne prosljeđuje klijentu (može sadržavati detalje zahtjeva).
            logger.LogWarning("Stripe returned {StatusCode} for {Path}.", (int)response.StatusCode, request.RequestUri?.AbsolutePath);
            response.Dispose();
            throw new ValidationException("Stripe nije prihvatio zahtjev. Provjerite Stripe test ključeve i pokušajte ponovo.");
        }
        return response;
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
