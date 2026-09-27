namespace GoBeyond.Core.Entities;

/// <summary>Šifarnik: spol.</summary>
public class Gender : BaseEntity, INamedEntity
{
    public string Name { get; set; } = string.Empty;
}
