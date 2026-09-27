namespace GoBeyond.Infrastructure.Services.Files;

/// <summary>
/// Formati lokacija fajlova koje čuva baza.
///   "/uploads/{kategorija}/{guid}.ext"    javni fajl (profilne slike, slike napretka) - servira ga UseStaticFiles (wwwroot).
///   "private://{kategorija}/{guid}.ext"   privatni fajl (certifikati) u Uploads:PrivateRoot - samo kroz autorizovani endpoint.
///   "seed://{kategorija}/{fajl}"          privatni demo fajl u Uploads:SeedFilesRoot (dio image-a).
/// </summary>
public static class FileLocations
{
    public const string PublicPrefix = "/uploads/";
    public const string PrivateScheme = "private://";
    public const string SeedScheme = "seed://";

    /// <summary>Stari (javni) format certifikata iz ranijih verzija baze - podržan dok se baza ne resetuje.</summary>
    public const string LegacySeedPrefix = "/seed/";
}
