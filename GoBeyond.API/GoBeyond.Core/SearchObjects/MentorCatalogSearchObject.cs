namespace GoBeyond.Core.SearchObjects;

public class MentorCatalogSearchObject
{
    public int? TrainingTypeId { get; set; }
    public string? Search { get; set; }

    /// <summary>rating | name | price</summary>
    public string? SortBy { get; set; }

    /// <summary>asc | desc</summary>
    public string? SortDirection { get; set; }
}
