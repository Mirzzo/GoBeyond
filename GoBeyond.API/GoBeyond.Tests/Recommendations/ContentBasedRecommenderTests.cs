using GoBeyond.Infrastructure.Services.Recommendations;

namespace GoBeyond.Tests.Recommendations;

public class ContentBasedRecommenderTests
{
    // Vrste treninga: 1 = Weightlifting, 2 = Calisthenics, 3 = Hybrid
    // Ciljevi: 10 = Mršavljenje, 11 = Mišićna masa, 12 = Snaga
    private static readonly int[] TrainingTypes = [1, 2, 3];
    private static readonly int[] Goals = [10, 11, 12];
    private static readonly Dictionary<int, int> None = new();

    private static ContentBasedRecommender CreateRecommender(int maxYears = 10) => new(TrainingTypes, Goals, maxYears);

    private static MentorFeatures Mentor(int id, int type, int[] specs, int years = 5, double rating = 4.5, int reviews = 10,
        Dictionary<int, int>? successfulGoals = null) =>
        new(id, type, specs, successfulGoals ?? None, years, rating, reviews);

    private static ClientFeatures Client(int? preferredType, int goal, int level = 2, Dictionary<int, int>? pastTypes = null) =>
        new(preferredType, pastTypes ?? None, goal, level, 4);

    [Fact]
    public void CosineSimilarity_OfIdenticalVectors_IsOne()
    {
        var similarity = ContentBasedRecommender.CosineSimilarity([1, 2, 3], [1, 2, 3]);
        Assert.Equal(1.0, similarity, 6);
    }

    [Fact]
    public void CosineSimilarity_OfOrthogonalVectors_IsZero()
    {
        Assert.Equal(0.0, ContentBasedRecommender.CosineSimilarity([1, 0, 0], [0, 1, 0]), 6);
    }

    [Fact]
    public void CosineSimilarity_IgnoresVectorLength()
    {
        // Kosinus mjeri ugao, ne dužinu: [1,1] i [5,5] su "isti smjer".
        Assert.Equal(1.0, ContentBasedRecommender.CosineSimilarity([1, 1], [5, 5]), 6);
    }

    [Fact]
    public void CosineSimilarity_WithZeroVector_IsZero()
    {
        Assert.Equal(0.0, ContentBasedRecommender.CosineSimilarity([0, 0, 0], [1, 2, 3]));
    }

    [Fact]
    public void CosineSimilarity_WithDifferentDimensions_Throws()
    {
        Assert.Throws<ArgumentException>(() => ContentBasedRecommender.CosineSimilarity([1, 2], [1, 2, 3]));
    }

    [Fact]
    public void Vectors_HaveOneDimensionPerTypeAndGoalPlusExperienceAndRating()
    {
        var recommender = CreateRecommender();
        Assert.Equal(TrainingTypes.Length + Goals.Length + 2, recommender.Dimensions);
        Assert.Equal(recommender.Dimensions, recommender.MentorVector(Mentor(1, 1, [10])).Length);
        Assert.Equal(recommender.Dimensions, recommender.ClientVector(Client(1, 10)).Length);
    }

    [Fact]
    public void Recommend_RanksMentorWithPreferredTypeAndGoalSpecializationFirst()
    {
        var recommender = CreateRecommender();
        var client = Client(preferredType: 3, goal: 10);
        var candidates = new[]
        {
            Mentor(1, type: 1, specs: [11], years: 10, rating: 5.0, reviews: 30), // najbolja ocjena, ali pogrešan tip i cilj
            Mentor(2, type: 3, specs: [10], years: 5, rating: 4.6, reviews: 8),  // tip + cilj
            Mentor(3, type: 3, specs: [11, 12], years: 3, rating: 4.0, reviews: 4) // samo tip
        };

        var ranked = recommender.Recommend(client, candidates, [], take: 3);

        Assert.Equal([2, 3, 1], ranked.Select(x => x.MentorProfileId));
        Assert.All(ranked, x => Assert.InRange(x.Score, 0.0, 1.0));
    }

    [Fact]
    public void Recommend_PrefersMentorWhoseSuccessfulClientsHadTheSameGoal()
    {
        var recommender = CreateRecommender();
        var client = Client(preferredType: 1, goal: 10);
        var withHistory = Mentor(1, 1, [10, 11], successfulGoals: new Dictionary<int, int> { [10] = 4 });
        var withoutHistory = Mentor(2, 1, [10, 11]);

        var ranked = recommender.Recommend(client, [withoutHistory, withHistory], [], take: 2);

        Assert.Equal(1, ranked[0].MentorProfileId);
        Assert.True(ranked[0].Score > ranked[1].Score);
    }

    [Fact]
    public void Recommend_AddsBonusForSimilarityToSuccessfulPastMentor()
    {
        var recommender = CreateRecommender();
        var client = Client(preferredType: null, goal: 12);
        var pastMentor = Mentor(99, type: 2, specs: [11]);
        // Oba kandidata imaju specijalizaciju za cilj klijenta; kandidat 1 je po tipu sličan ranijem mentoru.
        var similarToPast = Mentor(1, type: 2, specs: [11, 12]);
        var differentFromPast = Mentor(2, type: 1, specs: [11, 12]);

        var withoutHistory = recommender.Recommend(client, [similarToPast, differentFromPast], [], take: 2);
        var withHistory = recommender.Recommend(client, [similarToPast, differentFromPast], [pastMentor], take: 2);

        Assert.Equal(withoutHistory[0].Score, withoutHistory[1].Score, 6);
        Assert.Equal(1, withHistory[0].MentorProfileId);
        Assert.Equal(99, withHistory[0].MostSimilarPastMentorId);
    }

    [Fact]
    public void Recommend_UsesPastTrainingTypesWhenClientHasNoPreference()
    {
        var recommender = CreateRecommender();
        var client = Client(preferredType: null, goal: 10, pastTypes: new Dictionary<int, int> { [2] = 2 });

        var ranked = recommender.Recommend(client, [Mentor(1, 1, [10]), Mentor(2, 2, [10])], [], take: 2);

        Assert.Equal(2, ranked[0].MentorProfileId);
    }

    [Fact]
    public void Recommend_RespectsTake()
    {
        var recommender = CreateRecommender();
        var candidates = Enumerable.Range(1, 10).Select(id => Mentor(id, 1 + id % 3, [10 + id % 3]));

        Assert.Equal(3, recommender.Recommend(Client(1, 10), candidates, [], take: 3).Count);
    }

    [Fact]
    public void BayesianRating_PullsMentorsWithFewReviewsTowardsPrior()
    {
        var oneReview = ContentBasedRecommender.BayesianRating(5.0, 1);
        var manyReviews = ContentBasedRecommender.BayesianRating(5.0, 50);

        Assert.True(oneReview < manyReviews);
        Assert.Equal(ContentBasedRecommender.RatingPrior, ContentBasedRecommender.BayesianRating(0, 0), 6);
    }

    [Fact]
    public void ClientVector_ExperienceExpectationGrowsWithFitnessLevel()
    {
        var recommender = CreateRecommender();
        var beginner = recommender.ClientVector(Client(1, 10, level: 1));
        var advanced = recommender.ClientVector(Client(1, 10, level: 4));
        var experienceIndex = TrainingTypes.Length + Goals.Length;

        Assert.True(advanced[experienceIndex] > beginner[experienceIndex]);
    }
}
