using System.Net;
using System.Text;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Services.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Payments;

/// <summary>
/// Stripe odgovori koji nisu greška konfiguracije: 409 (isti Idempotency-Key je još u obradi) je "pokušajte ponovo"
/// (409), a timeout HttpClient-a je ista 400 greška komunikacije kao prekid veze - nikad neobrađen izuzetak.
/// </summary>
public sealed class StripeGatewayResilienceTests
{
    private static readonly IOptions<PaymentOptions> Options = Microsoft.Extensions.Options.Options.Create(new PaymentOptions
    {
        SecretKey = "sk_test_fake", PublishableKey = "pk_test_fake", Currency = "usd", ApiBaseUrl = "https://stripe.test/v1/"
    });

    [Theory]
    [InlineData("invalid_request_error", "idempotency_key_in_use")]
    [InlineData("invalid_request_error", "lock_timeout")]
    public async Task Conflict409_MeansThePreviousRequestIsStillProcessing(string type, string code)
    {
        var gateway = Gateway(new StubHandler(HttpStatusCode.Conflict,
            System.Text.Json.JsonSerializer.Serialize(new { error = new { type, code, message = "Stripe error." } })));

        var create = await Assert.ThrowsAsync<ConflictException>(() => gateway.CreatePaymentIntentAsync(29.99m, "c@test.ba",
            new Dictionary<string, string>(), "create-intent:1", CancellationToken.None));
        var refund = await Assert.ThrowsAsync<ConflictException>(() => gateway.RefundAsync("pi_1", "refund:pi_1", CancellationToken.None));

        Assert.Equal(PaymentService.StillProcessing, create.Message);
        Assert.Equal(PaymentService.StillProcessing, refund.Message);
    }

    [Fact]
    public async Task HttpClientTimeout_IsAStripeCommunicationError()
    {
        var gateway = Gateway(new HangingHandler(), TimeSpan.FromMilliseconds(200));

        var error = await Assert.ThrowsAsync<ValidationException>(() => gateway.GetPaymentIntentAsync("pi_1", CancellationToken.None));

        Assert.Equal("Komunikacija sa Stripe servisom nije uspjela. Pokušajte ponovo.", error.Message);
    }

    [Fact]
    public async Task CallerCancellation_IsNotTurnedIntoAValidationError()
    {
        var gateway = Gateway(new HangingHandler());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.GetPaymentIntentAsync("pi_1", cancellation.Token));
    }

    private static StripePaymentGateway Gateway(HttpMessageHandler handler, TimeSpan? timeout = null) =>
        new(new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(30) }, Options, NullLogger<StripePaymentGateway>.Instance);

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    /// <summary>Kao Stripe (ili proxy) koji prihvati konekciju i nikad ne odgovori.</summary>
    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }
}
