using System.ComponentModel.DataAnnotations;

namespace GoBeyond.Core.Validation;

/// <summary>Lista mora imati između Min i Max elemenata (npr. specijalizacije).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ItemCountAttribute(int min, int max) : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null) return min == 0;
        if (value is not System.Collections.ICollection collection) return false;
        return collection.Count >= min && collection.Count <= max;
    }
}
