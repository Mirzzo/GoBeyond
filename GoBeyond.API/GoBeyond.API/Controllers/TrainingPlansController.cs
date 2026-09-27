using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Plans;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Services.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

/// <summary>
/// Trening planovi: mentor ih kreira i uređuje (prijelazi statusa idu kroz State Machine),
/// klijent čita trenutni plan i evidentira treninge.
/// </summary>
[ApiController]
[Route("api/training-plans")]
public sealed class TrainingPlansController(ITrainingPlanService planService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.MentorOnly)]
    public Task<List<PlanSummaryDto>> Get([FromQuery] PlanSearchObject searchObject, CancellationToken cancellationToken) =>
        planService.GetMentorPlansAsync(User.GetUserId(), searchObject, cancellationToken);

    [HttpGet("by-subscription/{subscriptionId:int}")]
    [Authorize(Policy = Policies.MentorOnly)]
    public Task<PlanDetailDto> GetBySubscription(int subscriptionId, CancellationToken cancellationToken) =>
        planService.GetBySubscriptionAsync(User.GetUserId(), subscriptionId, cancellationToken);

    [HttpGet("my-current")]
    [Authorize(Policy = Policies.ClientOnly)]
    public Task<PlanDetailDto> GetMyCurrent(CancellationToken cancellationToken) =>
        planService.GetMyCurrentAsync(User.GetUserId(), cancellationToken);

    /// <summary>Mentor vlasnik, klijent vlasnik ili administrator (read-only i kad pretplata nije aktivna).</summary>
    [HttpGet("{id:int}")]
    [Authorize]
    public Task<PlanDetailDto> GetById(int id, CancellationToken cancellationToken) =>
        planService.GetByIdAsync(User.GetUserId(), User.GetRole(), id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.MentorOnly)]
    public Task<PlanDetailDto> Create([FromBody] CreatePlanRequest request, CancellationToken cancellationToken) =>
        planService.CreateAsync(User.GetUserId(), request, cancellationToken);

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.MentorOnly)]
    public Task<PlanDetailDto> Update(int id, [FromBody] UpdatePlanRequest request, CancellationToken cancellationToken) =>
        planService.UpdateAsync(User.GetUserId(), id, request, cancellationToken);

    [HttpPut("{id:int}/days/{dayOfWeek:int}")]
    [Authorize(Policy = Policies.MentorOnly)]
    public Task<PlanDetailDto> UpsertDay(int id, int dayOfWeek, [FromBody] UpsertDayPlanRequest request, CancellationToken cancellationToken) =>
        planService.UpsertDayAsync(User.GetUserId(), id, dayOfWeek, request, cancellationToken);

    [HttpDelete("{id:int}/days/{dayOfWeek:int}")]
    [Authorize(Policy = Policies.MentorOnly)]
    public Task<PlanDetailDto> RemoveDay(int id, int dayOfWeek, CancellationToken cancellationToken) =>
        planService.RemoveDayAsync(User.GetUserId(), id, dayOfWeek, cancellationToken);

    [HttpPut("{id:int}/publish")]
    [Authorize(Policy = Policies.MentorOnly)]
    public Task<PlanDetailDto> Publish(int id, CancellationToken cancellationToken) =>
        planService.PublishAsync(User.GetUserId(), id, cancellationToken);

    [HttpPut("{id:int}/archive")]
    [Authorize(Policy = Policies.MentorOnly)]
    public Task<PlanDetailDto> Archive(int id, CancellationToken cancellationToken) =>
        planService.ArchiveAsync(User.GetUserId(), id, cancellationToken);

    [HttpPost("{planId:int}/days/{dayOfWeek:int}/sessions")]
    [Authorize(Policy = Policies.ClientOnly)]
    public Task<TrainingSessionItemDto> LogSession(int planId, int dayOfWeek, [FromBody] LogTrainingSessionRequest request,
        CancellationToken cancellationToken) =>
        planService.LogSessionAsync(User.GetUserId(), planId, dayOfWeek, request, cancellationToken);

    [HttpGet("{planId:int}/sessions")]
    [Authorize]
    public Task<List<TrainingSessionItemDto>> GetSessions(int planId, CancellationToken cancellationToken) =>
        planService.GetSessionsAsync(User.GetUserId(), User.GetRole(), planId, cancellationToken);
}
