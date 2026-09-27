namespace GoBeyond.API.Validation;

public static class ErrorMessages
{
    public const string Unauthorized = "Niste prijavljeni ili je sesija istekla. Prijavite se ponovo.";
    public const string Forbidden = "Nemate pravo pristupa ovoj akciji.";
    public const string SessionInvalid = "Sesija više nije važeća. Prijavite se ponovo.";
    public const string RouteNotFound = "Tražena adresa ne postoji.";
    public const string MethodNotAllowed = "HTTP metoda nije dozvoljena za ovu adresu.";
    public const string UnsupportedMediaType = "Format zahtjeva nije podržan (očekuje se JSON ili multipart/form-data).";
}
