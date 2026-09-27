namespace GoBeyond.Core.SearchObjects;

/// <summary>Pretraga šifarnika po nazivu (<c>?name=&amp;page=&amp;pageSize=</c>).</summary>
public class ReferenceSearchObject : BaseSearchObject
{
    public string? Name { get; set; }
}
