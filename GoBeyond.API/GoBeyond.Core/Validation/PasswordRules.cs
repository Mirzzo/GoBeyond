namespace GoBeyond.Core.Validation;

/// <summary>Pravila lozinke (8-64 znaka, barem jedno slovo i jedan broj).</summary>
public static class PasswordRules
{
    public const int MinLength = 8;
    public const int MaxLength = 64;
    public const string Message = "Lozinka mora imati 8–64 znaka, uključujući barem jedno slovo i jedan broj.";

    public static bool IsValid(string? password) =>
        !string.IsNullOrEmpty(password)
        && password.Length is >= MinLength and <= MaxLength
        && password.Any(char.IsLetter)
        && password.Any(char.IsDigit);
}
