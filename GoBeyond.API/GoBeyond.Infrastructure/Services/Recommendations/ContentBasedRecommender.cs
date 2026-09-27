namespace GoBeyond.Infrastructure.Services.Recommendations;

/// <summary>Karakteristike mentora koje ulaze u vektor (content-based filtering).</summary>
/// <param name="SuccessfulClientGoalCounts">Broj uspješnih klijenata po cilju (FitnessGoalId → broj).</param>
public sealed record MentorFeatures(
    int MentorProfileId,
    int TrainingTypeId,
    IReadOnlyCollection<int> SpecializationGoalIds,
    IReadOnlyDictionary<int, int> SuccessfulClientGoalCounts,
    int YearsOfExperience,
    double AverageRating,
    int ReviewCount);

/// <summary>Profil klijenta pretvoren u "očekivanja" u istom prostoru karakteristika.</summary>
/// <param name="PastTrainingTypeCounts">Vrste treninga mentora s kojima je klijent ranije sarađivao (TrainingTypeId → broj).</param>
/// <param name="FitnessLevelRank">Redoslijed nivoa spreme (1 = početnik).</param>
public sealed record ClientFeatures(
    int? PreferredTrainingTypeId,
    IReadOnlyDictionary<int, int> PastTrainingTypeCounts,
    int FitnessGoalId,
    int FitnessLevelRank,
    int MaxFitnessLevelRank);

public sealed record RecommendationScore(
    int MentorProfileId,
    double Score,
    double ContentSimilarity,
    int? MostSimilarPastMentorId,
    double MostSimilarPastMentorSimilarity);

/// <summary>
/// Content-based filtering preporuka mentora (čista matematika, bez baze - lako se testira).
///
/// Svaki mentor i klijent se predstavljaju vektorom u istom prostoru:
///   [ vrsta treninga (one-hot, T dimenzija) | ciljevi (G dimenzija) | iskustvo | ocjena ]
/// Blokovi "vrsta" i "ciljevi" se normalizuju na jediničnu dužinu pa množe težinom bloka,
/// tako da nijedan blok ne dominira samo zbog broja dimenzija.
///
/// Mentor:  vrsta = one-hot; ciljevi = 0.6·specijalizacije + 0.4·udio ciljeva njegovih uspješnih klijenata;
///          iskustvo = godine / max godina; ocjena = Bayesov prosjek / 5.
/// Klijent: vrsta = 0.7·preferirana vrsta + 0.3·vrste ranijih mentora; ciljevi = one-hot primarnog cilja;
///          iskustvo = očekivanje raste s nivoom spreme (0.3 → 1.0); ocjena = 1 (svi žele dobro ocijenjene mentore).
///
/// Sličnost = kosinusna sličnost vektora (0..1 jer su sve komponente nenegativne).
/// Ako klijent ima uspješne ranije saradnje, dodaje se bonus: sličnost kandidata s najsličnijim
/// ranijim mentorom (mentor↔mentor kosinus), sa težinom 0.2.
/// </summary>
public sealed class ContentBasedRecommender
{
    public const double TrainingTypeWeight = 1.0;
    public const double GoalWeight = 1.0;
    public const double ExperienceWeight = 0.5;
    public const double RatingWeight = 0.5;
    public const double SpecializationShare = 0.6;
    public const double SuccessfulClientsShare = 0.4;
    public const double PreferredTypeShare = 0.7;
    public const double PastTypeShare = 0.3;
    public const double PastCollaborationBonusWeight = 0.2;
    public const double RatingPrior = 3.5;
    public const int RatingPriorWeight = 2;

    private readonly IReadOnlyList<int> _trainingTypeIds;
    private readonly IReadOnlyList<int> _goalIds;
    private readonly double _maxYearsOfExperience;

    public ContentBasedRecommender(IEnumerable<int> trainingTypeIds, IEnumerable<int> fitnessGoalIds, int maxYearsOfExperience)
    {
        _trainingTypeIds = trainingTypeIds.Distinct().OrderBy(x => x).ToList();
        _goalIds = fitnessGoalIds.Distinct().OrderBy(x => x).ToList();
        _maxYearsOfExperience = Math.Max(1, maxYearsOfExperience);
    }

    public int Dimensions => _trainingTypeIds.Count + _goalIds.Count + 2;

    public double[] MentorVector(MentorFeatures mentor)
    {
        var typeBlock = _trainingTypeIds.Select(id => id == mentor.TrainingTypeId ? 1.0 : 0.0).ToArray();

        var successfulTotal = mentor.SuccessfulClientGoalCounts.Values.Sum();
        var goalBlock = _goalIds.Select(id =>
        {
            var specialized = mentor.SpecializationGoalIds.Contains(id) ? 1.0 : 0.0;
            if (successfulTotal == 0) return specialized;
            var share = mentor.SuccessfulClientGoalCounts.TryGetValue(id, out var count) ? (double)count / successfulTotal : 0.0;
            return SpecializationShare * specialized + SuccessfulClientsShare * share;
        }).ToArray();

        var experience = Math.Min(1.0, mentor.YearsOfExperience / _maxYearsOfExperience);
        var rating = BayesianRating(mentor.AverageRating, mentor.ReviewCount) / 5.0;

        return Compose(typeBlock, goalBlock, experience, rating);
    }

    public double[] ClientVector(ClientFeatures client)
    {
        var pastTotal = client.PastTrainingTypeCounts.Values.Sum();
        var hasPreferred = client.PreferredTrainingTypeId is not null;
        var typeBlock = _trainingTypeIds.Select(id =>
        {
            var preferred = client.PreferredTrainingTypeId == id ? 1.0 : 0.0;
            var past = pastTotal > 0 && client.PastTrainingTypeCounts.TryGetValue(id, out var count) ? (double)count / pastTotal : 0.0;
            if (!hasPreferred) return past;
            return pastTotal == 0 ? preferred : PreferredTypeShare * preferred + PastTypeShare * past;
        }).ToArray();

        var goalBlock = _goalIds.Select(id => id == client.FitnessGoalId ? 1.0 : 0.0).ToArray();

        var levelSpan = Math.Max(1, client.MaxFitnessLevelRank - 1);
        var levelShare = Math.Clamp((client.FitnessLevelRank - 1) / (double)levelSpan, 0.0, 1.0);
        var experienceExpectation = 0.3 + 0.7 * levelShare;

        return Compose(typeBlock, goalBlock, experienceExpectation, 1.0);
    }

    /// <summary>Kosinusna sličnost: (a·b) / (|a|·|b|). Za nula-vektor vraća 0.</summary>
    public static double CosineSimilarity(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        if (a.Count != b.Count) throw new ArgumentException("Vektori moraju imati isti broj dimenzija.");
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Count; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        if (normA == 0 || normB == 0) return 0;
        return Math.Clamp(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)), 0.0, 1.0);
    }

    /// <summary>Rangira kandidate za klijenta (najveći score prvi).</summary>
    public IReadOnlyList<RecommendationScore> Recommend(ClientFeatures client, IEnumerable<MentorFeatures> candidates,
        IReadOnlyCollection<MentorFeatures> successfulPastMentors, int take)
    {
        var clientVector = ClientVector(client);
        var pastVectors = successfulPastMentors.Select(m => (m.MentorProfileId, Vector: MentorVector(m))).ToList();

        return candidates
            .Select(candidate =>
            {
                var vector = MentorVector(candidate);
                var content = CosineSimilarity(clientVector, vector);

                int? bestPastId = null;
                var bestPast = 0.0;
                foreach (var (pastId, pastVector) in pastVectors.Where(x => x.MentorProfileId != candidate.MentorProfileId))
                {
                    var similarity = CosineSimilarity(vector, pastVector);
                    if (similarity > bestPast) (bestPast, bestPastId) = (similarity, pastId);
                }

                var score = pastVectors.Count == 0
                    ? content
                    : (1 - PastCollaborationBonusWeight) * content + PastCollaborationBonusWeight * bestPast;
                return new RecommendationScore(candidate.MentorProfileId, Math.Round(score, 4), content, bestPastId, bestPast);
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.MentorProfileId)
            .Take(take)
            .ToList();
    }

    /// <summary>Mentori najsličniji zadanom mentoru (mentor↔mentor kosinusna sličnost).</summary>
    public IReadOnlyList<(int MentorProfileId, double Similarity)> FindSimilar(MentorFeatures target,
        IEnumerable<MentorFeatures> others, int take)
    {
        var targetVector = MentorVector(target);
        return others
            .Where(x => x.MentorProfileId != target.MentorProfileId)
            .Select(x => (x.MentorProfileId, Similarity: Math.Round(CosineSimilarity(targetVector, MentorVector(x)), 4)))
            .OrderByDescending(x => x.Similarity)
            .ThenBy(x => x.MentorProfileId)
            .Take(take)
            .ToList();
    }

    /// <summary>Bayesov prosjek: mentori s malo recenzija se "vuku" prema prosjeku 3.5.</summary>
    public static double BayesianRating(double averageRating, int reviewCount) =>
        (averageRating * reviewCount + RatingPrior * RatingPriorWeight) / (reviewCount + RatingPriorWeight);

    private static double[] Compose(double[] typeBlock, double[] goalBlock, double experience, double rating)
    {
        Normalize(typeBlock, TrainingTypeWeight);
        Normalize(goalBlock, GoalWeight);
        return [.. typeBlock, .. goalBlock, ExperienceWeight * experience, RatingWeight * rating];
    }

    private static void Normalize(double[] block, double weight)
    {
        var norm = Math.Sqrt(block.Sum(x => x * x));
        if (norm == 0) return;
        for (var i = 0; i < block.Length; i++) block[i] = block[i] / norm * weight;
    }
}
