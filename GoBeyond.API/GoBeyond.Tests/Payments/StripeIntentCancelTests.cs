using System.Net;
using System.Text;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Services.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Payments;

/// <summary>Otkazivanje PaymentIntent-a na Stripe-u i vrijeme naplate (latest_charge) iz GET odgovora.</summary>
public sealed class StripeIntentCancelTests
{
    private const string CanceledJson = """
        {"id":"pi_123","object":"payment_intent","client_secret":"pi_123_secret","status":"canceled","amount":2999,"currency":"usd",
         "metadata":{"subscriptionId":"42","purpose":"Initial"}}
        """;

    private const string SucceededJson = """
        {"id":"pi_123","object":"payment_intent","client_secret":"pi_123_secret","status":"succeeded","amount":2999,"currency":"usd",
         "metadata":{"subscriptionId":"42","purpose":"Initial"},"latest_charge":{"id":"ch_1","object":"charge","created":1790000000}}
        """;

    private readonly ScriptedHandler _handler = new();
    private readonly StripePaymentGateway _gateway;

    public StripeIntentCancelTests()
    {
        var options = Options.Create(new PaymentOptions
        {
            SecretKey = "sk_test_fake", PublishableKey = "pk_test_fake", Currency = "usd", ApiBaseUrl = "https://stripe.test/v1/"
        });
        _gateway = new StripePaymentGateway(new HttpClient(_handler), options, NullLogger<StripePaymentGateway>.Instance);
    }

    [Fact]
    public async Task Cancel_PostsToTheCancelEndpointWithIdempotencyKey()
    {
        _handler.Responses.Enqueue((HttpStatusCode.OK, CanceledJson));

        var intent = await _gateway.CancelPaymentIntentAsync("pi_123", "cancel:pi_123", CancellationToken.None);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/v1/payment_intents/pi_123/cancel", request.Url);
        Assert.Equal("cancel:pi_123", request.IdempotencyKey);
        Assert.Equal("canceled", intent.Status);
    }

    [Fact]
    public async Task Cancel_OfAnIntentThatWasJustPaid_ReturnsItsCurrentState()
    {
        // Stvarni Stripe odgovor kad se otkazuje već plaćen PaymentIntent.
        _handler.Responses.Enqueue((HttpStatusCode.BadRequest, System.Text.Json.JsonSerializer.Serialize(new
        {
            error = new { type = "invalid_request_error", code = "payment_intent_unexpected_state", message = "You cannot cancel this PaymentIntent." }
        })));
        _handler.Responses.Enqueue((HttpStatusCode.OK, SucceededJson));

        var intent = await _gateway.CancelPaymentIntentAsync("pi_123", "cancel:pi_123", CancellationToken.None);

        Assert.Equal("succeeded", intent.Status);
        Assert.Equal([HttpMethod.Post, HttpMethod.Get], _handler.Requests.Select(x => x.Method));
    }

    [Fact]
    public async Task GetIntent_ExpandsTheLatestChargeAndReadsTheChargeTime()
    {
        _handler.Responses.Enqueue((HttpStatusCode.OK, SucceededJson));

        var intent = await _gateway.GetPaymentIntentAsync("pi_123", CancellationToken.None);

        Assert.Contains("expand%5B%5D=latest_charge", Assert.Single(_handler.Requests).Url);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1790000000).UtcDateTime, intent.ChargedAt);
    }

    [Fact]
    public void IntentFromWebhook_WithChargeIdOnly_HasNoChargeTime()
    {
        using var json = System.Text.Json.JsonDocument.Parse("""{"id":"pi_1","status":"succeeded","amount":100,"latest_charge":"ch_1"}""");

        Assert.Null(PaymentIntentInfo.FromJson(json.RootElement).ChargedAt);
    }

    private sealed record CapturedRequest(HttpMethod Method, string Url, string? IdempotencyKey);

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public Queue<(HttpStatusCode Status, string Body)> Responses { get; } = new();
        public List<CapturedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var key = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null;
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!.ToString(), key));
            var (status, body) = Responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
