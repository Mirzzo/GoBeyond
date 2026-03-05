using GoBeyond.Core.Enums;

namespace GoBeyond.Core.SearchObjects;

public class MentorSearchObject : BaseSearchObject
{
    public MentorCategory? Category { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool OrderByRatingDesc { get; set; }
}
