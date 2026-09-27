namespace GoBeyond.Core.SearchObjects;

/// <summary>Bazni SearchObject za generički BaseCRUD obrazac (paginacija).</summary>
public class BaseSearchObject
{
    private const int MaxPageSize = 100;
    private int _page = 1;
    private int _pageSize = 50;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 1 : Math.Min(value, MaxPageSize);
    }
}
