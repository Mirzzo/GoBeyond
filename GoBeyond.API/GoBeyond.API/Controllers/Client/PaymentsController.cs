using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Subscriptions;
using GoBeyond.Infrastructure.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Client;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(IPaymentService paymentService) : ControllerBase
{
    /// <summary>Stripe PaymentIntent: Initial (PendingPayment) ili Renewal (Active, +30 dana).</summary>
    [HttpPost("create-intent")]
    [Authorize(Policy = Policies.ClientOnly)]
    public Task<PaymentIntentDto> CreateIntent([FromBody] CreatePaymentIntentRequest request, CancellationToken cancellationToken) =>
        paymentService.CreateIntentAsync(User.GetUserId(), request, cancellationToken);

    /// <summary>Backend provjerava PaymentIntent na Stripe-u i tek tada označava uplatu uspješnom.</summary>
    [HttpPost("{paymentId:int}/confirm")]
    [Authorize(Policy = Policies.ClientOnly)]
    public Task<SubscriptionDetailDto> Confirm(int paymentId, CancellationToken cancellationToken) =>
        paymentService.ConfirmAsync(User.GetUserId(), paymentId, cancellationToken);

    /// <summary>Stripe webhook (anonimno, ali se Stripe-Signature potpis obavezno verifikuje).</summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        await paymentService.HandleWebhookAsync(payload, Request.Headers["Stripe-Signature"].ToString(), cancellationToken);
        return Ok();
    }
}
