using GoBeyond.Core.DTOs.Plans;
using GoBeyond.Core.DTOs.Progress;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.Files;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Files;
using GoBeyond.Infrastructure.Services.Plans;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoBeyond.Infrastructure.Services.Progress;

public interface IProgressService
{
    Task<List<int>> GetYearsAsync(int clientUserId, CancellationToken cancellationToken = default);
    Task<List<ProgressEntryItemDto>> GetByYearAsync(int clientUserId, int? year, CancellationToken cancellationToken = default);
    Task<ProgressEntryItemDto> GetAsync(int clientUserId, int year, int month, CancellationToken cancellationToken = default);
    Task<ProgressEntryItemDto> UpsertAsync(int clientUserId, int year, int month, UpsertProgressRequest request, CancellationToken cancellationToken = default);
    Task<ProgressEntryItemDto> UploadPhotoAsync(int clientUserId, int year, int month, FileUpload file, CancellationToken cancellationToken = default);
    Task<PlanDetailDto> GetPlanSnapshotAsync(int clientUserId, int year, int month, CancellationToken cancellationToken = default);
    Task<List<ProgressChartPointDto>> GetChartAsync(int clientUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Mjesečna historija treninga klijenta (slika + parametri + snapshot plana "HISTORIJA PLANA"). Tekući mjesec i godina se
/// računaju u vremenskoj zoni platforme (Lifecycle:TimeZoneId), kao u aplikaciji: prvog u mjesecu poslije ponoći je novi
/// mjesec već otvoren, iako je po UTC-u još prethodni. Sat se zadaje samo u testovima.
/// </summary>
public sealed class ProgressService(
    GoBeyondDbContext db,
    ITrainingPlanService plans,
    IFileStorageService files,
    IOptions<LifecycleOptions> lifecycleOptions,
    TimeProvider? clock = null) : IProgressService
{
    public const string EntryNotFound = "Za odabrani mjesec nema unosa napretka.";

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<List<int>> GetYearsAsync(int clientUserId, CancellationToken cancellationToken = default)
    {
        var clientId = await GetClientIdAsync(clientUserId, cancellationToken);
        var years = await db.ProgressEntries.Where(x => x.ClientProfileId == clientId)
            .Select(x => x.Year).Distinct().ToListAsync(cancellationToken);
        years.Add(PlatformNow().Year);
        return years.Distinct().OrderByDescending(x => x).ToList();
    }

    public async Task<List<ProgressEntryItemDto>> GetByYearAsync(int clientUserId, int? year, CancellationToken cancellationToken = default)
    {
        var clientId = await GetClientIdAsync(clientUserId, cancellationToken);
        var selectedYear = year ?? PlatformNow().Year;
        var entries = await db.ProgressEntries.AsNoTracking()
            .Where(x => x.ClientProfileId == clientId && x.Year == selectedYear)
            .OrderBy(x => x.Month)
            .ToListAsync(cancellationToken);
        return entries.Select(ToItem).ToList();
    }

    public async Task<ProgressEntryItemDto> GetAsync(int clientUserId, int year, int month, CancellationToken cancellationToken = default)
    {
        EnsureValidPeriod(year, month);
        var clientId = await GetClientIdAsync(clientUserId, cancellationToken);
        var entry = await db.ProgressEntries.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.ClientProfileId == clientId && x.Year == year && x.Month == month, cancellationToken)
                    ?? throw new NotFoundException(EntryNotFound);
        return ToItem(entry);
    }

    public async Task<ProgressEntryItemDto> UpsertAsync(int clientUserId, int year, int month, UpsertProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureValidPeriod(year, month);
        var now = _clock.GetUtcNow().UtcDateTime;
        var today = DomainTexts.PlatformTime(now, lifecycleOptions.Value.TimeZoneId);
        if (year > today.Year || (year == today.Year && month > today.Month))
            throw new ValidationException("Nije moguće unijeti napredak za budući mjesec.");

        var clientId = await GetClientIdAsync(clientUserId, cancellationToken);
        var entry = await db.ProgressEntries
            .FirstOrDefaultAsync(x => x.ClientProfileId == clientId && x.Year == year && x.Month == month, cancellationToken);

        if (entry is null)
        {
            entry = new ProgressEntry { ClientProfileId = clientId, Year = year, Month = month, CreatedAt = now };
            // Pri kreiranju se snima kopija plana koji klijent trenutno koristi ("HISTORIJA PLANA"), ali samo za tekući
            // mjesec - za prošle mjesece trenutni plan tada možda još nije ni postojao, pa se snapshot ne prilaže.
            var isCurrentMonth = year == today.Year && month == today.Month;
            if (isCurrentMonth && await plans.FindCurrentPlanAsync(clientId, cancellationToken) is { } plan)
            {
                entry.TrainingPlanId = plan.Id;
                entry.PlanSnapshotJson = PlanMapper.ToSnapshotJson(plan);
            }
            db.ProgressEntries.Add(entry);
        }

        entry.WeightKg = request.WeightKg;
        entry.Measurements = request.Measurements.Trim();
        entry.Strength = request.Strength.Trim();
        entry.Conditioning = request.Conditioning.Trim();
        entry.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return ToItem(entry);
    }

    public async Task<ProgressEntryItemDto> UploadPhotoAsync(int clientUserId, int year, int month, FileUpload file,
        CancellationToken cancellationToken = default)
    {
        EnsureValidPeriod(year, month);
        var clientId = await GetClientIdAsync(clientUserId, cancellationToken);
        var entry = await db.ProgressEntries
                        .FirstOrDefaultAsync(x => x.ClientProfileId == clientId && x.Year == year && x.Month == month, cancellationToken)
                    ?? throw new NotFoundException("Prvo unesite parametre napretka za ovaj mjesec, pa dodajte sliku.");

        var url = await files.SavePublicAsync(file, "progress", UploadKind.Image, "file", cancellationToken);
        var previous = entry.PhotoUrl;
        entry.PhotoUrl = url;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        files.Delete(previous);
        return ToItem(entry);
    }

    public async Task<PlanDetailDto> GetPlanSnapshotAsync(int clientUserId, int year, int month, CancellationToken cancellationToken = default)
    {
        EnsureValidPeriod(year, month);
        var clientId = await GetClientIdAsync(clientUserId, cancellationToken);
        var json = await db.ProgressEntries
            .Where(x => x.ClientProfileId == clientId && x.Year == year && x.Month == month)
            .Select(x => x.PlanSnapshotJson)
            .FirstOrDefaultAsync(cancellationToken);
        return PlanMapper.FromSnapshotJson(json) ?? throw new NotFoundException("Za ovaj mjesec nije sačuvan plan.");
    }

    public async Task<List<ProgressChartPointDto>> GetChartAsync(int clientUserId, CancellationToken cancellationToken = default)
    {
        var clientId = await GetClientIdAsync(clientUserId, cancellationToken);
        return await db.ProgressEntries.AsNoTracking()
            .Where(x => x.ClientProfileId == clientId)
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .Select(x => new ProgressChartPointDto { Year = x.Year, Month = x.Month, WeightKg = x.WeightKg })
            .ToListAsync(cancellationToken);
    }

    public static ProgressEntryItemDto ToItem(ProgressEntry entry) => new()
    {
        Id = entry.Id,
        Year = entry.Year,
        Month = entry.Month,
        MonthName = BosnianCalendar.MonthName(entry.Month),
        PhotoUrl = entry.PhotoUrl,
        WeightKg = entry.WeightKg,
        Measurements = entry.Measurements,
        Strength = entry.Strength,
        Conditioning = entry.Conditioning,
        HasPlanSnapshot = !string.IsNullOrEmpty(entry.PlanSnapshotJson),
        CreatedAt = entry.CreatedAt,
        UpdatedAt = entry.UpdatedAt
    };

    private DateTime PlatformNow() => DomainTexts.PlatformTime(_clock.GetUtcNow().UtcDateTime, lifecycleOptions.Value.TimeZoneId);

    private static void EnsureValidPeriod(int year, int month)
    {
        var errors = new ValidationErrorCollector();
        errors.Require(year is >= 2000 and <= 2100, "year", "Godina mora biti između 2000 i 2100.");
        errors.Require(month is >= 1 and <= 12, "month", "Mjesec mora biti broj od 1 do 12.");
        errors.ThrowIfAny();
    }

    private async Task<int> GetClientIdAsync(int clientUserId, CancellationToken cancellationToken) =>
        await db.ClientProfiles.Where(x => x.UserId == clientUserId).Select(x => (int?)x.Id).FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(DomainTexts.ProfileMissing);
}
