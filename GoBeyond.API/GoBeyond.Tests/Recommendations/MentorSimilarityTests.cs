using GoBeyond.Infrastructure.Services.Recommendations;

namespace GoBeyond.Tests.Recommendations;

public class MentorSimilarityTests
{
    private static readonly ContentBasedRecommender Recommender = new([1, 2, 3], [10, 11, 12, 13], maxYearsOfExperience: 12);
    private static readonly Dictionary<int, int> None = new();

    private static MentorFeatures Mentor(int id, int type, int[] specs, int years = 6, double rating = 4.5, int reviews = 6) =>
        new(id, type, specs, None, years, rating, reviews);

    [Fact]
    public void FindSimilar_ReturnsMentorWithSameTypeAndSpecializationsFirst()
    {
        var target = Mentor(1, type: 1, specs: [10, 12]);
        var others = new[]
        {
            Mentor(2, type: 2, specs: [11, 13]),
            Mentor(3, type: 1, specs: [10, 12], years: 7),
            Mentor(4, type: 1, specs: [11])
        };

        var similar = Recommender.FindSimilar(target, others, take: 3);

        Assert.Equal([3, 4, 2], similar.Select(x => x.MentorProfileId));
        Assert.True(similar[0].Similarity > 0.95);
    }

    [Fact]
    public void FindSimilar_NeverReturnsTheTargetItself()
    {
        var target = Mentor(1, 1, [10]);
        var similar = Recommender.FindSimilar(target, [target, Mentor(2, 1, [10])], take: 5);

        Assert.DoesNotContain(similar, x => x.MentorProfileId == 1);
        Assert.Single(similar);
    }

    [Fact]
    public void MentorSimilarity_IsSymmetric()
    {
        var a = Recommender.MentorVector(Mentor(1, 1, [10, 11], years: 3, rating: 4.0, reviews: 2));
        var b = Recommender.MentorVector(Mentor(2, 3, [11, 12], years: 10, rating: 4.9, reviews: 20));

        Assert.Equal(ContentBasedRecommender.CosineSimilarity(a, b), ContentBasedRecommender.CosineSimilarity(b, a), 10);
    }

    [Fact]
    public void FindSimilar_RespectsTake()
    {
        var target = Mentor(1, 1, [10]);
        var others = Enumerable.Range(2, 8).Select(id => Mentor(id, 1 + id % 3, [10 + id % 4]));

        Assert.Equal(3, Recommender.FindSimilar(target, others, take: 3).Count);
    }
}
