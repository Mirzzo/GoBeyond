using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.DTOs.Common;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Admin;

public interface IAdminMentorService
{
    Task<List<AdminMentorDto>> GetMentorsAsync(AdminMentorSearchObject search, CancellationToken cancellationToken = default);
    Task<List<MentorRequestDto>> GetRequestsAsync(MentorRequestSearchObject search, CancellationToken cancellationToken = default);
    Task<MentorRequestDetailDto> GetRequestAsync(int mentorProfileId, CancellationToken cancellationToken = default);
    Task<MessageResponse> ApproveAsync(int mentorProfileId, CancellationToken cancellationToken = default);
    Task<MessageResponse> RejectAsync(int mentorProfileId, RejectRequest request, CancellationToken cancellationToken = default);
    Task<CertificateDto> VerifyCertificateAsync(int certificateId, CancellationToken cancellationToken = default);
    Task<List<CertificateDto>> GetCertificatesAsync(int mentorProfileId, CancellationToken cancellationToken = default);
    Task<List<AdminClientDto>> GetClientsAsync(AdminClientSearchObject search, CancellationToken cancellationToken = default);
}

public sealed class AdminMentorService(GoBeyondDbContext db, INotificationSender notifications) : IAdminMentorService
{
    public async Task<List<AdminMentorDto>> GetMentorsAsync(AdminMentorSearchObject search, CancellationToken cancellationToken = default)
    {
        var query = db.MentorProfiles.AsNoTracking()
            .Where(x => x.Status == MentorApprovalStatus.Approved && !x.User.IsDeleted);
        if (search.Search.NormalizeSearch() is { } term)
            query = query.Where(x => (x.User.FirstName + " " + x.User.LastName).Contains(term) ||
                                     x.User.Username.Contains(term) || x.User.Email.Contains(term) ||
                                     (x.Nickname != null && x.Nickname.Contains(term)));
        if (search.TrainingTypeId is { } typeId) query = query.Where(x => x.TrainingTypeId == typeId);
        if (search.IsActive is { } isActive) query = query.Where(x => x.User.IsActive == isActive);

        var rows = await query
            .OrderBy(x => x.User.FirstName).ThenBy(x => x.User.LastName)
            .Select(x => new
            {
                Dto = new AdminMentorDto
                {
                    UserId = x.UserId,
                    MentorProfileId = x.Id,
                    FullName = x.User.FirstName + " " + x.User.LastName,
                    Nickname = x.Nickname,
                    Username = x.User.Username,
                    Email = x.User.Email,
                    ProfileImageUrl = x.User.ProfileImageUrl,
                    TrainingTypeName = x.TrainingType.Name,
                    MonthlyPrice = x.MonthlyPrice,
                    ReviewCount = x.Reviews.Count,
                    ActiveSubscribers = x.Subscriptions.Count(s => s.Status == SubscriptionStatus.Active),
                    IsActive = x.User.IsActive
                },
                Average = x.Reviews.Average(r => (double?)r.Rating)
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x =>
        {
            x.Dto.AverageRating = RoundRating(x.Average);
            return x.Dto;
        }).ToList();
    }

    public async Task<List<MentorRequestDto>> GetRequestsAsync(MentorRequestSearchObject search, CancellationToken cancellationToken = default)
    {
        var query = db.MentorProfiles.AsNoTracking()
            .Where(x => x.Status == MentorApprovalStatus.Pending && !x.User.IsDeleted);
        if (search.Search.NormalizeSearch() is { } term)
            query = query.Where(x => (x.User.FirstName + " " + x.User.LastName).Contains(term) || x.User.Email.Contains(term));
        if (search.TrainingTypeId is { } typeId) query = query.Where(x => x.TrainingTypeId == typeId);

        return await query.OrderBy(x => x.User.CreatedAt)
            .Select(x => new MentorRequestDto
            {
                MentorProfileId = x.Id,
                UserId = x.UserId,
                FullName = x.User.FirstName + " " + x.User.LastName,
                Email = x.User.Email,
                TrainingTypeName = x.TrainingType.Name,
                YearsOfExperience = x.YearsOfExperience,
                RequestedAt = x.User.CreatedAt,
                CertificateCount = x.Certificates.Count
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<MentorRequestDetailDto> GetRequestAsync(int mentorProfileId, CancellationToken cancellationToken = default)
    {
        var mentor = await db.MentorProfiles.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.TrainingType)
            .Include(x => x.Certificates)
            .Include(x => x.Specializations).ThenInclude(x => x.FitnessGoal)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == mentorProfileId && !x.User.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(DomainTexts.MentorNotFound);

        return new MentorRequestDetailDto
        {
            MentorProfileId = mentor.Id,
            UserId = mentor.UserId,
            FullName = mentor.User.FullName,
            Email = mentor.User.Email,
            TrainingTypeName = mentor.TrainingType.Name,
            YearsOfExperience = mentor.YearsOfExperience,
            RequestedAt = mentor.User.CreatedAt,
            CertificateCount = mentor.Certificates.Count,
            Nickname = mentor.Nickname,
            Bio = mentor.Bio,
            DateOfBirth = mentor.User.DateOfBirth,
            Age = ProfileMapper.Age(mentor.User.DateOfBirth),
            PhoneNumber = mentor.User.PhoneNumber,
            MonthlyPrice = mentor.MonthlyPrice,
            SpecializationNames = mentor.Specializations.Select(x => x.FitnessGoal.Name).OrderBy(x => x).ToList(),
            ProfileImageUrl = mentor.User.ProfileImageUrl,
            Certificates = mentor.Certificates.OrderBy(x => x.UploadedAt).Select(ProfileMapper.ToCertificate).ToList()
        };
    }

    public async Task<MessageResponse> ApproveAsync(int mentorProfileId, CancellationToken cancellationToken = default)
    {
        var mentor = await LoadPendingAsync(mentorProfileId, cancellationToken);
        mentor.Status = MentorApprovalStatus.Approved;
        mentor.RejectionReason = null;
        mentor.ReviewedAt = DateTime.UtcNow;

        notifications.Notify(mentor.User, NotificationType.MentorApproved, "Mentorski nalog je odobren",
            "Administrator je odobrio vaš mentorski nalog. Sada se možete prijaviti u desktop aplikaciju i primati zahtjeve klijenata.",
            sendEmail: true);
        await db.SaveChangesAsync(cancellationToken);
        return new MessageResponse($"Mentor {mentor.User.FullName} je odobren.");
    }

    public async Task<MessageResponse> RejectAsync(int mentorProfileId, RejectRequest request, CancellationToken cancellationToken = default)
    {
        var mentor = await LoadPendingAsync(mentorProfileId, cancellationToken);
        var reason = request.Reason.Trim();
        mentor.Status = MentorApprovalStatus.Rejected;
        mentor.RejectionReason = reason;
        mentor.ReviewedAt = DateTime.UtcNow;

        notifications.Notify(mentor.User, NotificationType.MentorRejected, "Zahtjev za mentorski nalog je odbijen",
            $"Vaš zahtjev za mentorski nalog je odbijen. Razlog: {reason}", sendEmail: true);
        await db.SaveChangesAsync(cancellationToken);
        return new MessageResponse($"Zahtjev mentora {mentor.User.FullName} je odbijen.");
    }

    public async Task<CertificateDto> VerifyCertificateAsync(int certificateId, CancellationToken cancellationToken = default)
    {
        var certificate = await db.MentorCertificates.FirstOrDefaultAsync(x => x.Id == certificateId, cancellationToken)
                          ?? throw new NotFoundException("Certifikat nije pronađen.");
        if (!certificate.IsVerified)
        {
            certificate.IsVerified = true;
            certificate.VerifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return ProfileMapper.ToCertificate(certificate);
    }

    public async Task<List<CertificateDto>> GetCertificatesAsync(int mentorProfileId, CancellationToken cancellationToken = default)
    {
        if (!await db.MentorProfiles.AnyAsync(x => x.Id == mentorProfileId, cancellationToken))
            throw new NotFoundException(DomainTexts.MentorNotFound);

        var certificates = await db.MentorCertificates.AsNoTracking()
            .Where(x => x.MentorProfileId == mentorProfileId)
            .OrderBy(x => x.UploadedAt)
            .ToListAsync(cancellationToken);
        return certificates.Select(ProfileMapper.ToCertificate).ToList();
    }

    public async Task<List<AdminClientDto>> GetClientsAsync(AdminClientSearchObject search, CancellationToken cancellationToken = default)
    {
        var query = db.ClientProfiles.AsNoTracking().Where(x => !x.User.IsDeleted && x.User.Role == UserRole.Client);
        if (search.Search.NormalizeSearch() is { } term)
            query = query.Where(x => (x.User.FirstName + " " + x.User.LastName).Contains(term) ||
                                     x.User.Username.Contains(term) || x.User.Email.Contains(term));
        if (search.FitnessGoalId is { } goalId) query = query.Where(x => x.FitnessGoalId == goalId);
        if (search.IsActive is { } isActive) query = query.Where(x => x.User.IsActive == isActive);

        return await query.OrderBy(x => x.User.FirstName).ThenBy(x => x.User.LastName)
            .Select(x => new AdminClientDto
            {
                UserId = x.UserId,
                ClientProfileId = x.Id,
                FullName = x.User.FirstName + " " + x.User.LastName,
                Username = x.User.Username,
                Email = x.User.Email,
                ProfileImageUrl = x.User.ProfileImageUrl,
                FitnessGoalName = x.FitnessGoal.Name,
                FitnessLevelName = x.FitnessLevel.Name,
                ActiveMentorName = x.Subscriptions
                    .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.AwaitingMentor)
                    .Select(s => s.MentorProfile.User.FirstName + " " + s.MentorProfile.User.LastName)
                    .FirstOrDefault(),
                IsActive = x.User.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public static decimal RoundRating(double? average) =>
        average is null ? 0 : Math.Round((decimal)average.Value, 1, MidpointRounding.AwayFromZero);

    private async Task<MentorProfile> LoadPendingAsync(int mentorProfileId, CancellationToken cancellationToken)
    {
        var mentor = await db.MentorProfiles.Include(x => x.User)
                         .FirstOrDefaultAsync(x => x.Id == mentorProfileId && !x.User.IsDeleted, cancellationToken)
                     ?? throw new NotFoundException(DomainTexts.MentorNotFound);
        if (mentor.Status != MentorApprovalStatus.Pending)
            throw new ValidationException("Ovaj zahtjev je već obrađen.");
        return mentor;
    }
}
