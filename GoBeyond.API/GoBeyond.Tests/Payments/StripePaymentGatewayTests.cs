using System.Net;
using System.Text;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Services.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Payments;

/// <summary>#3 (Idempotency-Key na svakom POST-u) i #4 (parsiranje metadata iz Stripe odgovora).</summary>
public class StripePaymentGatewayTests
{
    private const string IntentJson = """
        {"id":"pi_123","object":"payment_intent","client_secret":"pi_123_secret_abc","status":"requires_payment_method",
         "amount":2999,"currency":"usd","metadata":{"subscriptionId":"42","purpose":"Initial"}}
        """;

    private readonly RecordingHandler _handler = new();
    private readonly StripePaymentGateway _gateway;

    public StripePaymentGatewayTests()
    {
        var options = Options.Create(new PaymentOptions
        {
            SecretKey = "sk_test_fake", PublishableKey = "pk_test_fake", Currency = "usd", ApiBaseUrl = "https://stripe.test/v1/"
        });
        _gateway = new StripePaymentGateway(new HttpClient(_handler), options, NullLogger<StripePaymentGateway>.Instance);
    }

    [Fact]
    public async Task CreatePaymentIntent_SendsIdempotencyKeyAndParsesMetadata()
    {
        var intent = await _gateway.CreatePaymentIntentAsync(29.99m, "client@gobeyond.ba",
            new Dictionary<string, string> { ["subscriptionId"] = "42", ["purpose"] = "Initial" }, "create-intent:42:Initial:1:2999",
            CancellationToken.None);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("create-intent:42:Initial:1:2999", request.IdempotencyKey);
        Assert.Contains("amount=2999", request.Body);
        Assert.Contains("metadata%5BsubscriptionId%5D=42", request.Body);
        Assert.Equal(("pi_123", 2999L, "usd"), (intent.Id, intent.AmountMinor, intent.Currency));
        Assert.Equal("42", intent.Metadata["subscriptionId"]);
        Assert.Equal("Initial", intent.Metadata["purpose"]);
    }

    [Fact]
    public async Task Refund_SendsIdempotencyKey()
    {
        await _gateway.RefundAsync("pi_123", "refund:7", CancellationToken.None);

        var request = Assert.Single(_handler.Requests);
        Assert.EndsWith("/v1/refunds", request.Url);
        Assert.Equal("refund:7", request.IdempotencyKey);
        Assert.Contains("payment_intent=pi_123", request.Body);
    }

    [Fact]
    public async Task GetPaymentIntent_IsReadOnlyWithoutIdempotencyKey()
    {
        await _gateway.GetPaymentIntentAsync("pi_123", CancellationToken.None);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Null(request.IdempotencyKey);
    }

    [Fact]
    public async Task Post_WithoutIdempotencyKey_IsRefused()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _gateway.RefundAsync("pi_123", "", CancellationToken.None));
        Assert.Empty(_handler.Requests);
    }

    private sealed record CapturedRequest(HttpMethod Method, string Url, string? IdempotencyKey, string Body);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var key = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null;
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!.ToString(), key, body));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(IntentJson, Encoding.UTF8, "application/json") };
        }
    }
}
