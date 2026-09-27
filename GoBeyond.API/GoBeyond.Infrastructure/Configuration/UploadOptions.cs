namespace GoBeyond.Infrastructure.Configuration;

public sealed class UploadOptions
{
    public const string SectionName = "Uploads";
    public long MaxFileSizeBytes { get; set; }
    public int MaxCertificatesPerUpload { get; set; }
    public string[] ImageExtensions { get; set; } = [];
    public string[] CertificateExtensions { get; set; } = [];

    /// <summary>Najveća veličina fajla u MB (za poruke korisniku).</summary>
    public string MaxFileSizeText => $"{MaxFileSizeBytes / (1024d * 1024d):0.#} MB";

    /// <summary>Poruka kada je cijeli zahtjev (multipart) prevelik.</summary>
    public string RequestTooLargeMessage =>
        $"Zahtjev je prevelik. Fajl može imati najviše {MaxFileSizeText}, a zahtjev najviše {MaxCertificatesPerUpload} takva fajla.";

    /// <summary>Poruka kada broj certifikata nije u dozvoljenom rasponu.</summary>
    public string CertificateCountMessage =>
        $"Priložite 1–{MaxCertificatesPerUpload} certifikata ({string.Join(", ", CertificateExtensions)}; najviše {MaxFileSizeText} po fajlu).";
}
