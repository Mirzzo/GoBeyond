using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.Files;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoBeyond.Infrastructure.Services.Mentors;

public interface IMentorCertificateService
{
    Task<List<CertificateDto>> GetMineAsync(int mentorUserId, CancellationToken cancellationToken = default);
    Task<List<CertificateDto>> AddAsync(int mentorUserId, IReadOnlyList<FileUpload> files, CancellationToken cancellationToken = default);
    Task DeleteAsync(int mentorUserId, int certificateId, CancellationToken cancellationToken = default);
}

public sealed class MentorCertificateService(
    GoBeyondDbContext db,
    IFileStorageService fileStorage,
    IOptions<UploadOptions> uploadOptions) : IMentorCertificateService
{
    public async Task<List<CertificateDto>> GetMineAsync(int mentorUserId, CancellationToken cancellationToken = default)
    {
        var certificates = await db.MentorCertificates.AsNoTracking()
            .Where(x => x.MentorProfile.UserId == mentorUserId)
            .OrderBy(x => x.UploadedAt)
            .ToListAsync(cancellationToken);
        return certificates.Select(ProfileMapper.ToCertificate).ToList();
    }

    public async Task<List<CertificateDto>> AddAsync(int mentorUserId, IReadOnlyList<FileUpload> files, CancellationToken cancellationToken = default)
    {
        var max = uploadOptions.Value.MaxCertificatesPerUpload;
        if (files.Count < 1 || files.Count > max)
            throw new ValidationException("files", $"Priložite 1–{max} certifikata (PDF, JPG ili PNG, najviše 5 MB po fajlu).");
        foreach (var file in files) fileStorage.Validate(file, UploadKind.Certificate, "files");

        var mentor = await db.MentorProfiles.FirstOrDefaultAsync(x => x.UserId == mentorUserId, cancellationToken)
                     ?? throw new NotFoundException(DomainTexts.ProfileMissing);

        var saved = new List<MentorCertificate>();
        try
        {
            foreach (var file in files)
            {
                var url = await fileStorage.SaveAsync(file, "certificates", UploadKind.Certificate, "files", cancellationToken);
                saved.Add(new MentorCertificate
                {
                    MentorProfileId = mentor.Id,
                    FileName = Path.GetFileName(file.FileName),
                    FileUrl = url,
                    UploadedAt = DateTime.UtcNow
                });
            }
            db.MentorCertificates.AddRange(saved);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            saved.ForEach(x => fileStorage.Delete(x.FileUrl));
            throw;
        }
        return saved.Select(ProfileMapper.ToCertificate).ToList();
    }

    public async Task DeleteAsync(int mentorUserId, int certificateId, CancellationToken cancellationToken = default)
    {
        var certificate = await db.MentorCertificates
                              .FirstOrDefaultAsync(x => x.Id == certificateId && x.MentorProfile.UserId == mentorUserId, cancellationToken)
                          ?? throw new NotFoundException("Certifikat nije pronađen.");

        var remaining = await db.MentorCertificates.CountAsync(x => x.MentorProfileId == certificate.MentorProfileId, cancellationToken);
        if (remaining <= 1)
            throw new ValidationException("Morate imati barem jedan certifikat. Dodajte novi prije brisanja ovog.");

        db.MentorCertificates.Remove(certificate);
        await db.SaveChangesAsync(cancellationToken);
        fileStorage.Delete(certificate.FileUrl);
    }
}
