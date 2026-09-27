using System.ComponentModel.DataAnnotations;

namespace GoBeyond.Core.Validation;

/// <summary>Datum rođenja: korisnik mora imati između MinAge i MaxAge godina.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class DateOfBirthAttribute(int minAge, int maxAge) : ValidationAttribute(
    $"Datum rođenja nije ispravan: morate imati između {minAge} i {maxAge} godina.")
{
    public override bool IsValid(object? value)
    {
        if (value is not DateOnly date) return value is null;
        var age = AgeCalculator.From(date, DateOnly.FromDateTime(DateTime.UtcNow));
        return age >= minAge && age <= maxAge;
    }
}
