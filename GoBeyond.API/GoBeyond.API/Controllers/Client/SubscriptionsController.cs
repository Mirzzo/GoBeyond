using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Subscriptions;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Client;

[ApiController]
[Route("api/subscriptions")]
[Authorize(Policy = Policies.ClientOnly)]
public sealed class SubscriptionsController(ISubscriptionService subscriptionService) : ControllerBase
{
    /// <summary>Kupovina plana: kreira pretplatu (PendingPayment) sa upitnikom za mentora.</summary>
    [HttpPost]
    public Task<SubscriptionDetailDto> Create([FromBody] CreateSubscriptionRequest request, CancellationToken cancellationToken) =>
        subscriptionService.CreateAsync(User.GetUserId(), request, cancellationToken);

    [HttpGet("my")]
    public Task<List<SubscriptionDto>> GetMine([FromQuery] SubscriptionStatus? status, CancellationToken cancellationToken) =>
        subscriptionService.GetMineAsync(User.GetUserId(), status, cancellationToken);

    [HttpGet("my/{id:int}")]
    public Task<SubscriptionDetailDto> GetMineById(int id, CancellationToken cancellationToken) =>
        subscriptionService.GetMineByIdAsync(User.GetUserId(), id, cancellationToken);

    /// <summary>Otkazivanje (PendingPayment ili Active), bez povrata novca.</summary>
    [HttpPost("{id:int}/cancel")]
    public Task<SubscriptionDto> Cancel(int id, CancellationToken cancellationToken) =>
        subscriptionService.CancelAsync(User.GetUserId(), id, cancellationToken);
}
