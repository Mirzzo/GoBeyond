using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.DTOs.Common;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Admin;

/// <summary>Mentori, zahtjevi za mentorski nalog (pregled certifikata), klijenti.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminMentorsController(IAdminMentorService mentorService) : ControllerBase
{
    [HttpGet("mentors")]
    public Task<List<AdminMentorDto>> GetMentors([FromQuery] AdminMentorSearchObject searchObject, CancellationToken cancellationToken) =>
        mentorService.GetMentorsAsync(searchObject, cancellationToken);

    [HttpGet("mentors/{mentorProfileId:int}/certificates")]
    public Task<List<CertificateDto>> GetCertificates(int mentorProfileId, CancellationToken cancellationToken) =>
        mentorService.GetCertificatesAsync(mentorProfileId, cancellationToken);

    [HttpGet("mentor-requests")]
    public Task<List<MentorRequestDto>> GetRequests([FromQuery] MentorRequestSearchObject searchObject, CancellationToken cancellationToken) =>
        mentorService.GetRequestsAsync(searchObject, cancellationToken);

    [HttpGet("mentor-requests/{mentorProfileId:int}")]
    public Task<MentorRequestDetailDto> GetRequest(int mentorProfileId, CancellationToken cancellationToken) =>
        mentorService.GetRequestAsync(mentorProfileId, cancellationToken);

    [HttpPut("mentor-requests/{mentorProfileId:int}/approve")]
    public Task<MessageResponse> Approve(int mentorProfileId, CancellationToken cancellationToken) =>
        mentorService.ApproveAsync(mentorProfileId, cancellationToken);

    [HttpPut("mentor-requests/{mentorProfileId:int}/reject")]
    public Task<MessageResponse> Reject(int mentorProfileId, [FromBody] RejectRequest request, CancellationToken cancellationToken) =>
        mentorService.RejectAsync(mentorProfileId, request, cancellationToken);

    [HttpPut("certificates/{id:int}/verify")]
    public Task<CertificateDto> VerifyCertificate(int id, CancellationToken cancellationToken) =>
        mentorService.VerifyCertificateAsync(id, cancellationToken);

    [HttpGet("clients")]
    public Task<List<AdminClientDto>> GetClients([FromQuery] AdminClientSearchObject searchObject, CancellationToken cancellationToken) =>
        mentorService.GetClientsAsync(searchObject, cancellationToken);
}
