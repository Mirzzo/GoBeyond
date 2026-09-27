using System.Globalization;
using GoBeyond.Core.DTOs.Mentors;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Mentors;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Recommendations;

public interface IRecommendationService
{
    Task<List<MentorRecommendationDto>> RecommendForClientAsync(int clientUserId, int take, CancellationToken cancellationToken = default);
    Task<List<MentorSummaryDto>> GetSimilarMentorsAsync(int mentorProfileId, int take, CancellationToken cancellationToken = default);
}

/// <summary>
/// Učitava podatke iz baze, gradi karakteristike (features) i poziva <see cref="ContentBasedRecommender"/>.
/// "Uspješna saradnja" = mentor je prihvatio pretplatu i ona je završena (Expired) ili ocijenjena sa 4+.
/// </summary>
public sealed class RecommendationService(GoBeyondDbContext db, IMentorCatalogService catalog) : IRecommendationService
{
    private const double StrongSimilarity = 0.75;

    public async Task<List<MentorRecommendationDto>> RecommendForClientAsync(int clientUserId, int take, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 20);
        var client = await db.ClientProfiles.AsNoTracking()
                         .Include(x => x.FitnessLevel).Include(x => x.FitnessGoal).Include(x => x.PreferredTrainingType)
                         .FirstOrDefaultAsync(x => x.UserId == clientUserId, cancellationToken)
                     ?? throw new NotFoundException(DomainTexts.ProfileMissing);

        var context = await LoadContextAsync(cancellationToken);

        var history = await db.Subscriptions.AsNoTracking()
            .Where(x => x.ClientProfileId == client.Id && x.AcceptedAt != null)
            .Select(x => new
            {
                x.MentorProfileId,
                x.MentorProfile.TrainingTypeId,
                Successful = x.Status == SubscriptionStatus.Expired || (x.Review != null && x.Review.Rating >= 4)
            })
            .ToListAsync(cancellationToken);
        var excluded = await db.Subscriptions.AsNoTracking()
            .Where(x => x.ClientProfileId == client.Id && QueryExtensions.OpenStatuses.Contains(x.Status))
            .Select(x => x.MentorProfileId)
            .ToListAsync(cancellationToken);

        var clientFeatures = new ClientFeatures(
            client.PreferredTrainingTypeId,
            history.GroupBy(x => x.TrainingTypeId).ToDictionary(g => g.Key, g => g.Count()),
            client.FitnessGoalId,
            client.FitnessLevel.SortOrder,
            context.MaxFitnessLevelRank);

        var successfulPast = history.Where(x => x.Successful).Select(x => x.MentorProfileId).Distinct()
            .Where(context.Features.ContainsKey)
            .Select(id => context.Features[id]).ToList();
        var candidates = context.Features.Values
            .Where(x => context.VisibleIds.Contains(x.MentorProfileId) && !excluded.Contains(x.MentorProfileId));

        var ranked = context.Recommender.Recommend(clientFeatures, candidates, successfulPast, take);
        var summaries = await catalog.GetSummariesAsync(ranked.Select(x => x.MentorProfileId).ToList(), cancellationToken);

        return ranked.Where(x => summaries.ContainsKey(x.MentorProfileId)).Select(x => new MentorRecommendationDto
        {
            Mentor = summaries[x.MentorProfileId],
            Score = Math.Round(x.Score, 3),
            Reasons = BuildReasons(x, context, clientFeatures, client.PreferredTrainingType?.Name, client.FitnessGoal.Name)
        }).ToList();
    }

    public async Task<List<MentorSummaryDto>> GetSimilarMentorsAsync(int mentorProfileId, int take, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 20);
        var context = await LoadContextAsync(cancellationToken);
        if (!context.VisibleIds.Contains(mentorProfileId) || !context.Features.TryGetValue(mentorProfileId, out var target))
            throw new NotFoundException(DomainTexts.MentorNotFound);

        var others = context.Features.Values.Where(x => context.VisibleIds.Contains(x.MentorProfileId));
        var similar = context.Recommender.FindSimilar(target, others, take);
        var summaries = await catalog.GetSummariesAsync(similar.Select(x => x.MentorProfileId).ToList(), cancellationToken);
        return similar.Where(x => summaries.ContainsKey(x.MentorProfileId)).Select(x => summaries[x.MentorProfileId]).ToList();
    }

    /// <summary>Čitljiva objašnjenja preporuke (na bosanskom), od najjačeg razloga.</summary>
    private static List<string> BuildReasons(RecommendationScore score, RecommenderContext context, ClientFeatures client,
        string? preferredTypeName, string goalName)
    {
        var mentor = context.Features[score.MentorProfileId];
        var reasons = new List<string>();
        var typeName = context.TrainingTypeNames.GetValueOrDefault(mentor.TrainingTypeId, string.Empty);

        if (client.PreferredTrainingTypeId == mentor.TrainingTypeId)
            reasons.Add($"Vrsta treninga koju preferirate: {preferredTypeName ?? typeName}");
        else if (client.PastTrainingTypeCounts.ContainsKey(mentor.TrainingTypeId))
            reasons.Add($"Vrsta treninga s kojom ste već radili: {typeName}");

        if (mentor.SpecializationGoalIds.Contains(client.FitnessGoalId))
            reasons.Add($"Specijalizovan za: {goalName}");

        if (mentor.SuccessfulClientGoalCounts.TryGetValue(client.FitnessGoalId, out var successCount) && successCount > 0)
            reasons.Add($"Uspješne saradnje s klijentima istog cilja ({successCount})");

        if (mentor.ReviewCount > 0 && mentor.AverageRating >= 4.0)
            reasons.Add($"Visoka ocjena klijenata ({mentor.AverageRating.ToString("0.0", CultureInfo.InvariantCulture)})");

        if (mentor.YearsOfExperience >= 5)
            reasons.Add($"Iskusan mentor ({mentor.YearsOfExperience} godina iskustva)");

        if (score.MostSimilarPastMentorId is { } pastId && score.MostSimilarPastMentorSimilarity >= StrongSimilarity &&
            context.MentorNames.TryGetValue(pastId, out var pastName))
            reasons.Add($"Sličan mentoru s kojim ste uspješno sarađivali: {pastName}");

        if (reasons.Count == 0) reasons.Add("Dobro se uklapa u vaš profil i ciljeve");
        return reasons.Take(4).ToList();
    }

    private async Task<RecommenderContext> LoadContextAsync(CancellationToken cancellationToken)
    {
        var trainingTypes = await db.TrainingTypes.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var goalIds = await db.FitnessGoals.AsNoTracking().Select(x => x.Id).ToListAsync(cancellationToken);
        var maxLevel = await db.FitnessLevels.AsNoTracking().MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? 1;

        var mentors = await db.MentorProfiles.AsNoTracking()
            .Where(x => x.Status == MentorApprovalStatus.Approved)
            .Select(x => new
            {
                x.Id,
                x.TrainingTypeId,
                x.YearsOfExperience,
                Name = x.User.FirstName + " " + x.User.LastName,
                Visible = x.User.IsActive && !x.User.IsDeleted,
                Specializations = x.Specializations.Select(s => s.FitnessGoalId).ToList(),
                Average = x.Reviews.Average(r => (double?)r.Rating),
                ReviewCount = x.Reviews.Count
            })
            .ToListAsync(cancellationToken);

        var successfulClients = await db.Subscriptions.AsNoTracking()
            .Where(x => x.AcceptedAt != null &&
                        (x.Status == SubscriptionStatus.Expired || (x.Review != null && x.Review.Rating >= 4)))
            .Select(x => new { x.MentorProfileId, x.ClientProfile.FitnessGoalId })
            .ToListAsync(cancellationToken);
        var goalsByMentor = successfulClients.GroupBy(x => x.MentorProfileId)
            .ToDictionary(g => g.Key, g => g.GroupBy(x => x.FitnessGoalId).ToDictionary(x => x.Key, x => x.Count()));

        var features = mentors.ToDictionary(x => x.Id, x => new MentorFeatures(
            x.Id,
            x.TrainingTypeId,
            x.Specializations,
            goalsByMentor.GetValueOrDefault(x.Id) ?? new Dictionary<int, int>(),
            x.YearsOfExperience,
            Math.Round(x.Average ?? 0, 1),
            x.ReviewCount));

        var recommender = new ContentBasedRecommender(trainingTypes.Keys, goalIds,
            mentors.Count == 0 ? 1 : mentors.Max(x => x.YearsOfExperience));

        return new RecommenderContext(
            recommender,
            features,
            mentors.Where(x => x.Visible).Select(x => x.Id).ToHashSet(),
            mentors.ToDictionary(x => x.Id, x => x.Name),
            trainingTypes,
            maxLevel);
    }

    private sealed record RecommenderContext(
        ContentBasedRecommender Recommender,
        Dictionary<int, MentorFeatures> Features,
        HashSet<int> VisibleIds,
        Dictionary<int, string> MentorNames,
        Dictionary<int, string> TrainingTypeNames,
        int MaxFitnessLevelRank);
}
