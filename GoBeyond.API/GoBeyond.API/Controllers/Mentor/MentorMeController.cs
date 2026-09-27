using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.DTOs.Common;
using GoBeyond.Core.DTOs.Mentors;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Services.Mentors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Mentor;

/// <summary>Mentorski panel (desktop): certifikati, zahtjevi za saradnju i pretplatnici.</summary>
[ApiController]
[Route("api/mentors/me")]
[Authorize(Policy = Policies.MentorOnly)]
public sealed class MentorMeController(
    IMentorCertificateService certificateService,
    ICollaborationService collaborationService) : ControllerBase
{
    [HttpGet("certificates")]
    public Task<List<CertificateDto>> GetCertificates(CancellationToken cancellationToken) =>
        certificateService.GetMineAsync(User.GetUserId(), cancellationToken);

    [HttpPost("certificates")]
    [Consumes("multipart/form-data")]
    public Task<List<CertificateDto>> AddCertificates([FromForm] List<IFormFile> files, CancellationToken cancellationToken) =>
        certificateService.AddAsync(User.GetUserId(), files.ToFileUploads(), cancellationToken);

    [HttpDelete("certificates/{id:int}")]
    public async Task<IActionResult> DeleteCertificate(int id, CancellationToken cancellationToken)
    {
        await certificateService.DeleteAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }

    [HttpGet("collaboration-requests")]
    public Task<List<CollaborationRequestDto>> GetRequests([FromQuery] string? search, CancellationToken cancellationToken) =>
        collaborationService.GetRequestsAsync(User.GetUserId(), search, cancellationToken);

    [HttpGet("collaboration-requests/{subscriptionId:int}")]
    public Task<ClientDescriptionDto> GetRequest(int subscriptionId, CancellationToken cancellationToken) =>
        collaborationService.GetRequestAsync(User.GetUserId(), subscriptionId, cancellationToken);

    [HttpPut("collaboration-requests/{subscriptionId:int}/accept")]
    public Task<CollaborationRequestDto> Accept(int subscriptionId, CancellationToken cancellationToken) =>
        collaborationService.AcceptAsync(User.GetUserId(), subscriptionId, cancellationToken);

    [HttpPut("collaboration-requests/{subscriptionId:int}/reject")]
    public Task<MessageResponse> Reject(int subscriptionId, [FromBody] RejectRequest request, CancellationToken cancellationToken) =>
        collaborationService.RejectAsync(User.GetUserId(), subscriptionId, request.Reason, cancellationToken);

    [HttpGet("subscribers")]
    public Task<List<SubscriberDto>> GetSubscribers([FromQuery] SubscriptionSearchObject searchObject, CancellationToken cancellationToken) =>
        collaborationService.GetSubscribersAsync(User.GetUserId(), searchObject, cancellationToken);

    [HttpGet("subscribers/{subscriptionId:int}")]
    public Task<SubscriberDetailDto> GetSubscriber(int subscriptionId, CancellationToken cancellationToken) =>
        collaborationService.GetSubscriberAsync(User.GetUserId(), subscriptionId, cancellationToken);
}
