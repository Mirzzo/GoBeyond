namespace GoBeyond.Infrastructure.Configuration;

public sealed class UploadOptions
{
    public const string SectionName = "Uploads";
    public long MaxFileSizeBytes { get; set; }
    public int MaxCertificatesPerUpload { get; set; }
    public string[] ImageExtensions { get; set; } = [];
    public string[] CertificateExtensions { get; set; } = [];
}
