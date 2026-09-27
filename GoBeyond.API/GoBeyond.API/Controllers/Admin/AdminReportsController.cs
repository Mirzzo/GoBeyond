using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Reports;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Admin;

/// <summary>Poslovni izvještaji (year/month su opcioni, default je tekući mjesec).</summary>
[ApiController]
[Route("api/admin/reports")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminReportsController(IReportService reportService) : ControllerBase
{
    [HttpGet("mentors")]
    public Task<MentorReportDto> Mentors([FromQuery] ReportSearchObject searchObject, CancellationToken cancellationToken) =>
        reportService.GetMentorReportAsync(searchObject, cancellationToken);

    [HttpGet("mentors/{mentorProfileId:int}")]
    public Task<MentorReportDetailDto> Mentor(int mentorProfileId, [FromQuery] int? year, [FromQuery] int? month, CancellationToken cancellationToken) =>
        reportService.GetMentorReportDetailAsync(mentorProfileId, year, month, cancellationToken);

    [HttpGet("clients")]
    public Task<ClientReportDto> Clients([FromQuery] ReportSearchObject searchObject, CancellationToken cancellationToken) =>
        reportService.GetClientReportAsync(searchObject, cancellationToken);

    [HttpGet("clients/{clientProfileId:int}")]
    public Task<ClientReportDetailDto> Client(int clientProfileId, [FromQuery] int? year, [FromQuery] int? month, CancellationToken cancellationToken) =>
        reportService.GetClientReportDetailAsync(clientProfileId, year, month, cancellationToken);

    [HttpGet("overview")]
    public Task<OverviewReportDto> Overview(CancellationToken cancellationToken) =>
        reportService.GetOverviewAsync(cancellationToken);
}
