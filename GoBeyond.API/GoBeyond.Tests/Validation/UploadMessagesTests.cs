using GoBeyond.API.Validation;
using GoBeyond.Infrastructure.Configuration;

namespace GoBeyond.Tests.Validation;

/// <summary>#7: poruke o veličini fajla se računaju iz Uploads konfiguracije (nisu hardkodirane).</summary>
public class UploadMessagesTests
{
    private static readonly UploadOptions Uploads = new()
    {
        MaxFileSizeBytes = 3 * 1024 * 1024,
        MaxCertificatesPerUpload = 4,
        CertificateExtensions = [".pdf", ".png"]
    };

    [Fact]
    public void Messages_UseConfiguredLimits()
    {
        Assert.Equal("3 MB", Uploads.MaxFileSizeText);
        Assert.Contains("3 MB", Uploads.RequestTooLargeMessage);
        Assert.Contains("1–4", Uploads.CertificateCountMessage);
        Assert.Contains(".pdf, .png", Uploads.CertificateCountMessage);
    }

    [Theory]
    [InlineData("Failed to read the request form. Multipart body length limit 27262976 exceeded.")]
    [InlineData("Failed to read the request form. Request body too large. The max request body size is 30000000 bytes.")]
    public void OversizedFormErrors_AreTranslatedToBosnianSizeMessage(string aspNetMessage)
    {
        Assert.Equal(Uploads.RequestTooLargeMessage, ValidationResponseFactory.Translate(aspNetMessage, null, Uploads));
    }
}
