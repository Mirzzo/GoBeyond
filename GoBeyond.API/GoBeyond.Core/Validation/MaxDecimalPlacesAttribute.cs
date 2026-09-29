using System.ComponentModel.DataAnnotations;

namespace GoBeyond.Core.Validation;

/// <summary>Decimalni broj smije imati najviše zadati broj decimalnih mjesta (npr. cijena na dvije decimale, kao u bazi).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class MaxDecimalPlacesAttribute(int decimalPlaces) : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not decimal number) return true;
        return decimal.Round(number, decimalPlaces) == number;
    }
}
