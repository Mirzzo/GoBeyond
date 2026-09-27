namespace GoBeyond.Core.Entities;

/// <summary>Šifarnik sa jedinstvenim nazivom (koristi generički BaseCRUD servis).</summary>
public interface INamedEntity
{
    string Name { get; set; }
}
