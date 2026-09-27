using System.Net;
using System.Net.Http.Json;
using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Tests.Certificates;

/// <summary>Integracijski testovi (WebApplicationFactory) za GET /api/certificates/{id}/file i privatno skladište certifikata.</summary>
public sealed class CertificateFileEndpointTests(GoBeyondApiFactory factory) : IClassFixture<GoBeyondApiFactory>
{
    private int CertificateId(string fileName) =>
        factory.Query(db => db.MentorCertificates.AsNoTracking().Single(x => x.FileName == fileName).Id);

    [Theory]
    [InlineData(TestUsers.MentorA, HttpStatusCode.OK)]         // mentor vlasnik
    [InlineData(TestUsers.Admin, HttpStatusCode.OK)]           // administrator
    [InlineData(TestUsers.MentorB, HttpStatusCode.Forbidden)]  // drugi mentor
    [InlineData(TestUsers.Client, HttpStatusCode.Forbidden)]   // klijent
    [InlineData(null, HttpStatusCode.Unauthorized)]            // anonimno
    public async Task CertificateFile_AuthorizationMatrix(string? username, HttpStatusCode expected)
    {
        var response = await factory.ClientFor(username).GetAsync($"/api/certificates/{CertificateId("certifikat-trener.pdf")}/file");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task CertificateFile_ReturnsPdfInlineWithOriginalName()
    {
        var response = await factory.ClientFor(TestUsers.MentorA).GetAsync($"/api/certificates/{CertificateId("certifikat-trener.pdf")}/file");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("inline; filename=\"certifikat-trener.pdf\"", response.Content.Headers.GetValues("Content-Disposition").Single());
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    [Fact]
    public async Task CertificateFile_PngHasImageContentType()
    {
        var response = await factory.ClientFor(TestUsers.Admin).GetAsync($"/api/certificates/{CertificateId("diploma.png")}/file");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CertificateFile_LegacyPublicLocationStillResolvesPrivately()
    {
        var response = await factory.ClientFor(TestUsers.Admin).GetAsync($"/api/certificates/{CertificateId("stari-format.pdf")}/file");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CertificateFile_UnknownIdOrMissingFile_Returns404()
    {
        var admin = factory.ClientFor(TestUsers.Admin);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/certificates/999999/file")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/certificates/{CertificateId("nestao.pdf")}/file")).StatusCode);
    }

    [Theory]
    [InlineData("/seed/certificates/mentor-certifikat.pdf")] // demo certifikati više nisu u wwwroot
    [InlineData("/uploads/certificates/stari.pdf")]          // stari uploadi se ne serviraju statički
    public async Task Certificates_AreNotServedAsStaticFiles(string path)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task PublicImages_AreStillServed()
    {
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/seed/avatars/mentor.png")).StatusCode);
    }

    [Fact]
    public async Task CertificateDtos_PointToProtectedEndpoint()
    {
        var mentorProfileId = factory.Query(db => db.MentorProfiles.AsNoTracking().Single(x => x.User.Username == TestUsers.MentorA).Id);

        var certificates = await factory.ClientFor(TestUsers.Admin)
            .GetFromJsonAsync<List<CertificateDto>>($"/api/admin/mentors/{mentorProfileId}/certificates");

        Assert.NotNull(certificates);
        Assert.All(certificates, c => Assert.Equal($"/api/certificates/{c.Id}/file", c.FileUrl));
    }

    [Fact]
    public async Task UploadedCertificate_IsStoredPrivatelyAndDownloadableOnlyByOwnerAndAdmin()
    {
        var owner = factory.ClientFor(TestUsers.MentorA);
        var pdf = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "SeedFiles", "certificates", "kenan-certifikat.pdf"));
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(pdf);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(file, "files", "licenca-čćž.pdf");

        var uploaded = await (await owner.PostAsync("/api/mentors/me/certificates", form)).Content.ReadFromJsonAsync<List<CertificateDto>>();

        var certificate = Assert.Single(uploaded!);
        Assert.Equal($"/api/certificates/{certificate.Id}/file", certificate.FileUrl);
        var storedLocation = factory.Query(db => db.MentorCertificates.AsNoTracking().Single(x => x.Id == certificate.Id).FileUrl);
        Assert.StartsWith("private://certificates/", storedLocation);
        var physical = Path.Combine(factory.PrivateRoot, storedLocation["private://".Length..]);
        Assert.True(File.Exists(physical));

        var download = await owner.GetAsync(certificate.FileUrl);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Contains("filename*=UTF-8''licenca-%C4%8D%C4%87%C5%BE.pdf", download.Content.Headers.GetValues("Content-Disposition").Single());
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.ClientFor(TestUsers.MentorB).GetAsync(certificate.FileUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await factory.CreateClient().GetAsync("/uploads/certificates/" + Path.GetFileName(physical))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/mentors/me/certificates/{certificate.Id}")).StatusCode);
        Assert.False(File.Exists(physical));
    }
}
