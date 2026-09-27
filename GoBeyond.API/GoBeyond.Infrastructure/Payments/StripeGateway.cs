using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace GoBeyond.Infrastructure.Payments;

public sealed record StripeIntent(string Id, string ClientSecret, string Status, long Amount, string Currency);

public sealed class StripeGateway(HttpClient http, IOptions<PaymentOptions> options)
{
    public async Task<StripeIntent> CreateAsync(int subscriptionId, decimal amount, string email,
        CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, "payment_intents");
        request.Headers.Add("Idempotency-Key", $"gobeyond-subscription-{subscriptionId}");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["amount"] = ToMinorUnits(amount).ToString(CultureInfo.InvariantCulture),
            ["currency"] = options.Value.Currency,
            ["payment_method_types[]"] = "card",
            ["receipt_email"] = email,
            ["metadata[subscriptionId]"] = subscriptionId.ToString(CultureInfo.InvariantCulture)
        });
        return await SendAsync(request, cancellationToken);
    }

    public async Task<StripeIntent> RetrieveAsync(string intentId, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, $"payment_intents/{Uri.EscapeDataString(intentId)}");
        return await SendAsync(request, cancellationToken);
    }

    public static long ToMinorUnits(decimal amount) => checked((long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        if (!options.Value.SecretKey.StartsWith("sk_", StringComparison.Ordinal))
            throw new InvalidOperationException("Stripe secret key is not configured. Configure Payments__SecretKey.");
        var request = new HttpRequestMessage(method, new Uri(new Uri(options.Value.ApiBaseUrl), path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.SecretKey);
        return request;
    }

    private async Task<StripeIntent> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await http.SendAsync(request, cancellationToken);
        // Stripe error bodies may contain request details; never forward them or log secrets.
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Stripe could not process the request. Check the test configuration and try again.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var data = json.RootElement;
        return new StripeIntent(data.GetProperty("id").GetString()!,
            data.GetProperty("client_secret").GetString() ?? string.Empty,
            data.GetProperty("status").GetString()!, data.GetProperty("amount").GetInt64(),
            data.GetProperty("currency").GetString()!);
    }

    public static bool VerifyWebhook(string payload, string header, string secret, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(header)) return false;
        var values = header.Split(',').Select(x => x.Trim().Split('=', 2)).Where(x => x.Length == 2).ToArray();
        var timestampText = values.FirstOrDefault(x => x[0] == "t")?[1];
        if (!long.TryParse(timestampText, out var timestamp) || timestamp < now.ToUnixTimeSeconds() - 300
            || timestamp > now.ToUnixTimeSeconds() + 300) return false;
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestampText}.{payload}"));
        foreach (var signature in values.Where(x => x[0] == "v1"))
        {
            try
            {
                if (CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature[1]))) return true;
            }
            catch (FormatException) { }
        }
        return false;
    }
}
