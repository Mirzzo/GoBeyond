namespace GoBeyond.Core.Validation;

/// <summary>Zajednički regex obrasci i poruke (sve poruke navode format i ograničenja).</summary>
public static class ValidationPatterns
{
    public const string Username = @"^[a-zA-Z0-9._]{3,30}$";
    public const string UsernameMessage = "Korisničko ime mora imati 3–30 znakova i može sadržavati samo slova, brojeve, tačku i donju crtu.";

    public const string Email = @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$";
    public const string EmailMessage = "Unesite validnu email adresu (npr. ime@domena.com).";

    public const string Phone = @"^\+387 ?6\d ?\d{3} ?\d{3,4}$";
    public const string PhoneMessage = "Telefon mora biti u formatu +387 6X XXX XXX.";

    public const string Name = @"^[\p{L}][\p{L} '\-]{1,49}$";
}
