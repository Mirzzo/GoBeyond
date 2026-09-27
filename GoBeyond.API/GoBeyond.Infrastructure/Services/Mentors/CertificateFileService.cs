using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Files;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Mentors;

/// <summary>Fajl za preuzimanje: fizička putanja, MIME tip i originalni naziv.</summary>
public sealed record FileDownload(string PhysicalPath, string ContentType, string FileName);

public interface ICertificateFileService
{
    /// <summary>Certifikat smiju preuzeti administrator i mentor vlasnik; ostali dobijaju 403, nepostojeći 404.</summary>
    Task<FileDownload> GetAsync(int certificateId, int userId, UserRole role, CancellationToken cancellationToken = default);
}

/// <summary>Siguran pristup certifikatima mentora (fajlovi su izvan wwwroot i nisu javno dostupni).</summary>
public sealed class CertificateFileService(GoBeyondDbContext db, IFileStorageService files) : ICertificateFileService
{
    public const string NotFound = "Certifikat nije pronađen.";
    public const string Forbidden = "Nemate pristup ovom certifikatu.";

    public async Task<FileDownload> GetAsync(int certificateId, int userId, UserRole role, CancellationToken cancellationToken = default)
    {
        var certificate = await db.MentorCertificates.AsNoTracking()
                              .Where(x => x.Id == certificateId)
                              .Select(x => new { x.FileName, x.FileUrl, OwnerUserId = x.MentorProfile.UserId })
                              .FirstOrDefaultAsync(cancellationToken)
                          ?? throw new NotFoundException(NotFound);

        var allowed = role == UserRole.Admin || (role == UserRole.Mentor && certificate.OwnerUserId == userId);
        if (!allowed) throw new ForbiddenException(Forbidden);

        var path = files.ResolvePrivatePath(certificate.FileUrl)
                   ?? throw new NotFoundException("Fajl certifikata nije pronađen na serveru.");
        return new FileDownload(path, ContentTypeFor(path), certificate.FileName);
    }

    public static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream"
    };
}
