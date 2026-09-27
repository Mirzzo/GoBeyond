using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Communication;
using GoBeyond.Infrastructure.Services.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Common;

/// <summary>Interni sistem poruka mentor ↔ klijent (nit = pretplata).</summary>
[ApiController]
[Route("api/messages")]
[Authorize]
public sealed class MessagesController(IMessageService messageService) : ControllerBase
{
    [HttpGet("threads")]
    public Task<List<MessageThreadDto>> GetThreads([FromQuery] string? search, CancellationToken cancellationToken) =>
        messageService.GetThreadsAsync(User.GetUserId(), User.GetRole(), search, cancellationToken);

    [HttpGet("threads/{subscriptionId:int}")]
    public Task<List<MessageDto>> GetThread(int subscriptionId, CancellationToken cancellationToken) =>
        messageService.GetThreadAsync(User.GetUserId(), User.GetRole(), subscriptionId, cancellationToken);

    [HttpPost("threads/{subscriptionId:int}")]
    public Task<MessageDto> Send(int subscriptionId, [FromBody] SendMessageRequest request, CancellationToken cancellationToken) =>
        messageService.SendAsync(User.GetUserId(), User.GetRole(), subscriptionId, request, cancellationToken);
}
