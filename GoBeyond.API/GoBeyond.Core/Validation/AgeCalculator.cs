namespace GoBeyond.Core.Validation;

public static class AgeCalculator
{
    public static int From(DateOnly dateOfBirth, DateOnly today)
    {
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age)) age--;
        return age;
    }
}
