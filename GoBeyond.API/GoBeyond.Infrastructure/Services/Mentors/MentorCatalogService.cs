using GoBeyond.Core.DTOs.Mentors;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Admin;
using GoBeyond.Infrastructure.Services.Payments;
using GoBeyond.Infrastructure.Services.Reviews;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Mentors;

public interface IMentorCatalogService
{
    Task<List<MentorSummaryDto>> GetMentorsAsync(MentorCatalogSearchObject search, CancellationToken cancellationToken = default);
    Task<MentorDetailDto> GetMentorAsync(int mentorProfileId, int? currentUserId, CancellationToken cancellationToken = default);
    Task<List<ReviewDto>> GetReviewsAsync(int mentorProfileId, int? currentUserId, CancellationToken cancellationToken = default);

    /// <summary>Sažeci vidljivih mentora za zadane ID-eve (koristi ih sistem preporuke).</summary>
    Task<Dictionary<int, MentorSummaryDto>> GetSummariesAsync(IReadOnlyCollection<int> mentorProfileIds, CancellationToken cancellationToken = default);
}

/// <summary>Javni pregled mentora (mobilna aplikacija): samo odobreni, aktivni i neobrisani mentori.</summary>
public sealed class MentorCatalogService(GoBeyondDbContext db, IPaymentGateway paymentGateway) : IMentorCatalogService
{
    public async Task<List<MentorSummaryDto>> GetMentorsAsync(MentorCatalogSearchObject search, CancellationToken cancellationToken = default)
    {
        var query = db.MentorProfiles.AsNoTracking().Visible();
        if (search.TrainingTypeId is { } typeId) query = query.Where(x => x.TrainingTypeId == typeId);
        if (search.Search.NormalizeSearch() is { } term)
            query = query.Where(x => x.User.FirstName.Contains(term) || x.User.LastName.Contains(term) ||
                                     (x.User.FirstName + " " + x.User.LastName).Contains(term) ||
                                     (x.Nickname != null && x.Nickname.Contains(term)));

        var mentors = await ProjectSummariesAsync(query, cancellationToken);
        return Sort(mentors, search.SortBy, search.SortDirection);
    }

    public async Task<MentorDetailDto> GetMentorAsync(int mentorProfileId, int? currentUserId, CancellationToken cancellationToken = default)
    {
        var summary = (await ProjectSummariesAsync(db.MentorProfiles.AsNoTracking().Visible().Where(x => x.Id == mentorProfileId), cancellationToken))
                      .FirstOrDefault() ?? throw new NotFoundException(DomainTexts.MentorNotFound);

        var extra = await db.MentorProfiles.AsNoTracking().Where(x => x.Id == mentorProfileId)
            .Select(x => new { x.Bio, Specializations = x.Specializations.Select(s => s.FitnessGoal.Name).ToList() })
            .FirstAsync(cancellationToken);

        var detail = new MentorDetailDto
        {
            Bio = extra.Bio,
            SpecializationNames = extra.Specializations.OrderBy(x => x).ToList(),
            Reviews = (await LoadReviewsAsync(mentorProfileId, currentUserId, 10, cancellationToken))
        };
        CopySummary(summary, detail);
        return detail;
    }

    public async Task<List<ReviewDto>> GetReviewsAsync(int mentorProfileId, int? currentUserId, CancellationToken cancellationToken = default)
    {
        if (!await db.MentorProfiles.Visible().AnyAsync(x => x.Id == mentorProfileId, cancellationToken))
            throw new NotFoundException(DomainTexts.MentorNotFound);
        return await LoadReviewsAsync(mentorProfileId, currentUserId, null, cancellationToken);
    }

    public async Task<Dictionary<int, MentorSummaryDto>> GetSummariesAsync(IReadOnlyCollection<int> mentorProfileIds,
        CancellationToken cancellationToken = default)
    {
        var summaries = await ProjectSummariesAsync(
            db.MentorProfiles.AsNoTracking().Visible().Where(x => mentorProfileIds.Contains(x.Id)), cancellationToken);
        return summaries.ToDictionary(x => x.MentorProfileId);
    }

    private async Task<List<ReviewDto>> LoadReviewsAsync(int mentorProfileId, int? currentUserId, int? take, CancellationToken cancellationToken)
    {
        var query = db.Reviews.AsNoTracking()
            .Include(x => x.ClientProfile).ThenInclude(x => x.User)
            .Where(x => x.MentorProfileId == mentorProfileId)
            .OrderByDescending(x => x.CreatedAt).AsQueryable();
        if (take is { } limit) query = query.Take(limit);
        var reviews = await query.ToListAsync(cancellationToken);
        return reviews.Select(x => ReviewService.ToDto(x, currentUserId)).ToList();
    }

    private async Task<List<MentorSummaryDto>> ProjectSummariesAsync(IQueryable<MentorProfile> query, CancellationToken cancellationToken)
    {
        var rows = await query.Select(x => new
        {
            x.Id,
            x.User.FirstName,
            x.User.LastName,
            x.Nickname,
            x.User.ProfileImageUrl,
            x.TrainingTypeId,
            TrainingTypeName = x.TrainingType.Name,
            Average = x.Reviews.Average(r => (double?)r.Rating),
            ReviewCount = x.Reviews.Count,
            x.MonthlyPrice,
            x.YearsOfExperience,
            x.User.DateOfBirth
        }).ToListAsync(cancellationToken);

        return rows.Select(x => new MentorSummaryDto
        {
            MentorProfileId = x.Id,
            FullName = $"{x.FirstName} {x.LastName}",
            Nickname = x.Nickname,
            ProfileImageUrl = x.ProfileImageUrl,
            TrainingTypeId = x.TrainingTypeId,
            TrainingTypeName = x.TrainingTypeName,
            AverageRating = AdminMentorService.RoundRating(x.Average),
            ReviewCount = x.ReviewCount,
            MonthlyPrice = x.MonthlyPrice,
            Currency = paymentGateway.Currency,
            YearsOfExperience = x.YearsOfExperience,
            Age = ProfileMapper.Age(x.DateOfBirth)
        }).ToList();
    }

    /// <summary>sortBy: rating (default, silazno), name (uzlazno), price (uzlazno); sortDirection mijenja smjer.</summary>
    private static List<MentorSummaryDto> Sort(List<MentorSummaryDto> mentors, string? sortBy, string? sortDirection)
    {
        var key = sortBy?.Trim().ToLowerInvariant() ?? "rating";
        var descending = sortDirection?.Trim().ToLowerInvariant() switch
        {
            "asc" => false,
            "desc" => true,
            _ => key == "rating"
        };

        IOrderedEnumerable<MentorSummaryDto> ordered = key switch
        {
            "name" => descending ? mentors.OrderByDescending(x => x.FullName) : mentors.OrderBy(x => x.FullName),
            "price" => descending ? mentors.OrderByDescending(x => x.MonthlyPrice) : mentors.OrderBy(x => x.MonthlyPrice),
            _ => descending
                ? mentors.OrderByDescending(x => x.AverageRating).ThenByDescending(x => x.ReviewCount)
                : mentors.OrderBy(x => x.AverageRating).ThenBy(x => x.ReviewCount)
        };
        return ordered.ThenBy(x => x.FullName).ToList();
    }

    private static void CopySummary(MentorSummaryDto source, MentorSummaryDto target)
    {
        target.MentorProfileId = source.MentorProfileId;
        target.FullName = source.FullName;
        target.Nickname = source.Nickname;
        target.ProfileImageUrl = source.ProfileImageUrl;
        target.TrainingTypeId = source.TrainingTypeId;
        target.TrainingTypeName = source.TrainingTypeName;
        target.AverageRating = source.AverageRating;
        target.ReviewCount = source.ReviewCount;
        target.MonthlyPrice = source.MonthlyPrice;
        target.Currency = source.Currency;
        target.YearsOfExperience = source.YearsOfExperience;
        target.Age = source.Age;
    }
}
