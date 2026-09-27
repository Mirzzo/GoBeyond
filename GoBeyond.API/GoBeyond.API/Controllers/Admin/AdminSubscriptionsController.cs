using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Admin;

[ApiController]
[Route("api/admin/subscriptions")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminSubscriptionsController(ISubscriptionService subscriptionService) : ControllerBase
{
    [HttpGet]
    public Task<List<AdminSubscriptionDto>> Get([FromQuery] SubscriptionSearchObject searchObject, CancellationToken cancellationToken) =>
        subscriptionService.GetAllAsync(searchObject, cancellationToken);

    [HttpPut("{id:int}/cancel")]
    public Task<AdminSubscriptionDto> Cancel(int id, [FromBody] CancelSubscriptionRequest request, CancellationToken cancellationToken) =>
        subscriptionService.AdminCancelAsync(id, request, cancellationToken);
}
