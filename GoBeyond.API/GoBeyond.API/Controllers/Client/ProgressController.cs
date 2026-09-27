using System.ComponentModel.DataAnnotations;
using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Plans;
using GoBeyond.Core.DTOs.Progress;
using GoBeyond.Infrastructure.Services.Progress;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Client;

/// <summary>Historija treninga: mjesečni unos (slika + težina/obimi/snaga/kondicija) i "HISTORIJA PLANA".</summary>
[ApiController]
[Route("api/progress")]
[Authorize(Policy = Policies.ClientOnly)]
public sealed class ProgressController(IProgressService progressService) : ControllerBase
{
    [HttpGet("years")]
    public Task<List<int>> GetYears(CancellationToken cancellationToken) =>
        progressService.GetYearsAsync(User.GetUserId(), cancellationToken);

    [HttpGet]
    public Task<List<ProgressEntryItemDto>> Get([FromQuery] int? year, CancellationToken cancellationToken) =>
        progressService.GetByYearAsync(User.GetUserId(), year, cancellationToken);

    [HttpGet("chart")]
    public Task<List<ProgressChartPointDto>> GetChart(CancellationToken cancellationToken) =>
        progressService.GetChartAsync(User.GetUserId(), cancellationToken);

    [HttpGet("{year:int}/{month:int}")]
    public Task<ProgressEntryItemDto> GetOne(int year, int month, CancellationToken cancellationToken) =>
        progressService.GetAsync(User.GetUserId(), year, month, cancellationToken);

    [HttpPut("{year:int}/{month:int}")]
    public Task<ProgressEntryItemDto> Upsert(int year, int month, [FromBody] UpsertProgressRequest request, CancellationToken cancellationToken) =>
        progressService.UpsertAsync(User.GetUserId(), year, month, request, cancellationToken);

    [HttpPost("{year:int}/{month:int}/photo")]
    [Consumes("multipart/form-data")]
    public Task<ProgressEntryItemDto> UploadPhoto(int year, int month,
        [Required(ErrorMessage = "Odaberite sliku napretka (JPG ili PNG, najviše 5 MB).")] IFormFile file,
        CancellationToken cancellationToken) =>
        progressService.UploadPhotoAsync(User.GetUserId(), year, month, file.ToFileUpload(), cancellationToken);

    [HttpGet("{year:int}/{month:int}/plan")]
    public Task<PlanDetailDto> GetPlanSnapshot(int year, int month, CancellationToken cancellationToken) =>
        progressService.GetPlanSnapshotAsync(User.GetUserId(), year, month, cancellationToken);
}
