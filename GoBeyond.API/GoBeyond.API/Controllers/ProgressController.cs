using GoBeyond.API.Extensions;
using GoBeyond.API.Utilities;
using GoBeyond.Core.DTOs;
using GoBeyond.Core.Entities;
using GoBeyond.Infrastructure.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.API.Controllers;

[Authorize(Policy = "ClientOnly")]
[ApiController]
[Route("api/progress")]
public class ProgressController(GoBeyondDbContext dbContext) : ControllerBase
{
    [HttpPost]
    public async Task<ProgressEntryDto> Create(
        [FromBody] CreateProgressEntryRequestDto request,
        CancellationToken cancellationToken)
    {
        var clientProfile = await GetCurrentClientProfileAsync(cancellationToken);

        var year = request.Year ?? DateTime.UtcNow.Year;
        var month = request.Month ?? DateTime.UtcNow.Month;

        var progressEntry = await dbContext.ProgressEntries
            .FirstOrDefaultAsync(
                x => x.ClientProfileId == clientProfile.Id &&
                    x.Year == year &&
                    x.Month == month,
                cancellationToken);

        if (progressEntry is null)
        {
            progressEntry = new ProgressEntry
            {
                ClientProfileId = clientProfile.Id,
                Year = year,
                Month = month
            };

            dbContext.ProgressEntries.Add(progressEntry);
        }

        progressEntry.Weight = request.Weight ?? progressEntry.Weight;
        progressEntry.Measurements = request.Measurements?.Trim() ?? progressEntry.Measurements;
        progressEntry.Strength = request.Strength?.Trim() ?? progressEntry.Strength;
        progressEntry.Conditioning = request.Conditioning?.Trim() ?? progressEntry.Conditioning;

        await dbContext.SaveChangesAsync(cancellationToken);
        return DtoMapper.ToProgressEntryDto(progressEntry);
    }

    [HttpGet]
    public async Task<ProgressHistoryResponseDto> GetHistory(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var clientProfile = await GetCurrentClientProfileAsync(cancellationToken);

        var query = dbContext.ProgressEntries
            .Where(x => x.ClientProfileId == clientProfile.Id);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.Measurements != null && x.Measurements.ToLower().Contains(normalizedSearch)) ||
                (x.Strength != null && x.Strength.ToLower().Contains(normalizedSearch)) ||
                (x.Conditioning != null && x.Conditioning.ToLower().Contains(normalizedSearch)) ||
                (x.PhotoUrl != null && x.PhotoUrl.ToLower().Contains(normalizedSearch)) ||
                x.Month.ToString().Contains(normalizedSearch) ||
                x.Year.ToString().Contains(normalizedSearch));
        }

        var entries = await query
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ToListAsync(cancellationToken);

        return new ProgressHistoryResponseDto(
            DtoMapper.ToProgressSummary(entries),
            entries.Select(DtoMapper.ToProgressEntryDto).ToList());
    }

    [HttpPost("photo")]
    public async Task<ProgressEntryDto> UploadPhoto(
        [FromBody] UploadProgressPhotoRequestDto request,
        CancellationToken cancellationToken)
    {
        var clientProfile = await GetCurrentClientProfileAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var progressEntry = await dbContext.ProgressEntries
            .FirstOrDefaultAsync(
                x => x.ClientProfileId == clientProfile.Id &&
                    x.Year == now.Year &&
                    x.Month == now.Month,
                cancellationToken);

        if (progressEntry is null)
        {
            progressEntry = new ProgressEntry
            {
                ClientProfileId = clientProfile.Id,
                Year = now.Year,
                Month = now.Month
            };

            dbContext.ProgressEntries.Add(progressEntry);
        }

        progressEntry.PhotoUrl = request.PhotoUrl.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        return DtoMapper.ToProgressEntryDto(progressEntry);
    }

    private async Task<ClientProfile> GetCurrentClientProfileAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        return await dbContext.ClientProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Client profile not found.");
    }
}
