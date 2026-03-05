namespace GoBeyond.Core.SearchObjects;

public class BaseSearchObject
{
    private const int MaxPageSize = 100;

    public int Page { get; set; } = 1;

    private int _pageSize = 20;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
    }

    public string? Query { get; set; }
}
