using GoBeyond.Core.DTOs.Auth;
using GoBeyond.Core.DTOs.Common;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.Files;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Security;
using GoBeyond.Infrastructure.Services.Files;
using GoBeyond.Infrastructure.Services.Notifications;
using GoBeyond.Infrastructure.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoBeyond.Infrastructure.Services.Auth;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RegisterClientAsync(RegisterClientRequest request, CancellationToken cancellationToken = default);
    Task<MessageResponse> RegisterMentorAsync(RegisterMentorRequest request, IReadOnlyList<FileUpload> certificates, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(int userId, string refreshToken, CancellationToken cancellationToken = default);
    Task<MessageResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}

public sealed class AuthService(
    GoBeyondDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService tokens,
    IUserAccountValidator accountValidator,
    IFileStorageService files,
    INotificationSender notifications,
    IOptions<JwtOptions> jwtOptions,
    IOptions<UploadOptions> uploadOptions) : IAuthService
{
    public const string InvalidCredentials = "Pogrešno korisničko ime ili lozinka.";
    public const string BlockedMessage = "Vaš nalog je blokiran. Kontaktirajte administratora.";
    public const string PendingMentorMessage = "Vaš mentorski nalog čeka odobrenje administratora.";
    public const string SessionExpired = "Sesija je istekla. Prijavite se ponovo.";

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var login = request.Username.Trim();
        var user = await db.Users.Include(x => x.MentorProfile)
            .FirstOrDefaultAsync(x => x.Username == login || x.Email == login, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException(InvalidCredentials);

        EnsureCanSignIn(user);
        user.LastLoginAt = DateTime.UtcNow;
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RegisterClientAsync(RegisterClientRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrorCollector();
        await accountValidator.ValidateAccountAsync(errors, request, null, cancellationToken);
        await accountValidator.ValidateClientAsync(errors, request.FitnessLevelId, request.FitnessGoalId,
            request.PreferredTrainingTypeId, string.Empty, cancellationToken);
        errors.ThrowIfAny();

        var user = CreateUser(request, UserRole.Client);
        user.ClientProfile = new ClientProfile
        {
            WeightKg = request.WeightKg,
            HeightCm = request.HeightCm,
            FitnessLevelId = request.FitnessLevelId,
            TrainingExperienceYears = request.TrainingExperienceYears,
            FitnessGoalId = request.FitnessGoalId,
            GoalDescription = string.IsNullOrWhiteSpace(request.GoalDescription) ? null : request.GoalDescription.Trim(),
            PreferredTrainingTypeId = request.PreferredTrainingTypeId
        };
        user.LastLoginAt = DateTime.UtcNow;
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        notifications.QueueEmail(user, "ClientRegistered", "Dobrodošli na GoBeyond",
            "Vaš klijentski nalog je uspješno kreiran. Pronađite mentora koji odgovara vašim ciljevima i započnite saradnju.");
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<MessageResponse> RegisterMentorAsync(RegisterMentorRequest request, IReadOnlyList<FileUpload> certificates,
        CancellationToken cancellationToken = default)
    {
        var maxCertificates = uploadOptions.Value.MaxCertificatesPerUpload;
        var errors = new ValidationErrorCollector();
        await accountValidator.ValidateAccountAsync(errors, request, null, cancellationToken);
        await accountValidator.ValidateMentorAsync(errors, request.TrainingTypeId, request.SpecializationIds, string.Empty, cancellationToken);
        errors.Require(certificates.Count >= 1 && certificates.Count <= maxCertificates, "certificates",
            uploadOptions.Value.CertificateCountMessage);
        errors.ThrowIfAny();

        foreach (var certificate in certificates)
            files.Validate(certificate, UploadKind.Certificate, "certificates");

        var user = CreateUser(request, UserRole.Mentor);
        var mentor = new MentorProfile
        {
            TrainingTypeId = request.TrainingTypeId,
            Nickname = string.IsNullOrWhiteSpace(request.Nickname) ? null : request.Nickname.Trim(),
            Bio = request.Bio.Trim(),
            YearsOfExperience = request.YearsOfExperience,
            MonthlyPrice = request.MonthlyPrice,
            Status = MentorApprovalStatus.Pending,
            Specializations = request.SpecializationIds.Distinct()
                .Select(id => new MentorSpecialization { FitnessGoalId = id }).ToList()
        };
        user.MentorProfile = mentor;

        var savedUrls = new List<string>();
        try
        {
            foreach (var certificate in certificates)
            {
                var url = await files.SavePrivateAsync(certificate, "certificates", UploadKind.Certificate, "certificates", cancellationToken);
                savedUrls.Add(url);
                mentor.Certificates.Add(new MentorCertificate
                {
                    FileName = Path.GetFileName(certificate.FileName),
                    FileUrl = url,
                    UploadedAt = DateTime.UtcNow
                });
            }

            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            savedUrls.ForEach(files.Delete);
            throw;
        }

        notifications.QueueEmail(user, "MentorRegistered", "Registracija je primljena",
            "Vaš zahtjev za mentorski nalog je primljen. Administrator će pregledati vaše certifikate i obavijestiti vas o odluci.");
        await db.SaveChangesAsync(cancellationToken);

        return new MessageResponse("Registracija je uspješna. Vaš nalog čeka odobrenje administratora.");
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = tokens.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens
            .Include(x => x.User).ThenInclude(x => x.MentorProfile)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedException(SessionExpired);

        EnsureCanSignIn(stored.User);
        stored.RevokedAt = DateTime.UtcNow; // rotacija: stari token se poništava
        return await IssueTokensAsync(stored.User, cancellationToken);
    }

    public async Task LogoutAsync(int userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = tokens.HashRefreshToken(refreshToken);
        await db.RefreshTokens
            .Where(x => x.UserId == userId && x.TokenHash == hash && x.RevokedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAt, DateTime.UtcNow), cancellationToken);
    }

    public async Task<MessageResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
                   ?? throw new NotFoundException(DomainTexts.UserNotFound);

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ValidationException("currentPassword", "Trenutna lozinka nije ispravna.");
        if (request.NewPassword == request.CurrentPassword)
            throw new ValidationException("newPassword", "Nova lozinka mora biti različita od trenutne.");

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        await db.SaveChangesAsync(cancellationToken);
        return new MessageResponse("Lozinka je uspješno promijenjena.");
    }

    /// <summary>Blokiran/obrisan korisnik i mentor koji nije odobren ne mogu se prijaviti (403).</summary>
    public static void EnsureCanSignIn(User user)
    {
        if (user.IsDeleted || !user.IsActive)
            throw new ForbiddenException(BlockedMessage);

        if (user.Role != UserRole.Mentor || user.MentorProfile is null) return;
        switch (user.MentorProfile.Status)
        {
            case MentorApprovalStatus.Pending:
                throw new ForbiddenException(PendingMentorMessage);
            case MentorApprovalStatus.Rejected:
                throw new ForbiddenException($"Vaš zahtjev za mentorski nalog je odbijen: {user.MentorProfile.RejectionReason}");
        }
    }

    private User CreateUser(RegisterRequestBase request, UserRole role) => new()
    {
        FirstName = request.FirstName.Trim(),
        LastName = request.LastName.Trim(),
        Username = request.Username.Trim(),
        Email = request.Email.Trim(),
        PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
        DateOfBirth = request.DateOfBirth,
        GenderId = request.GenderId,
        PasswordHash = passwordHasher.Hash(request.Password),
        Role = role,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var (accessToken, expiresAt) = tokens.CreateAccessToken(user);
        var refreshToken = tokens.CreateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokens.HashRefreshToken(refreshToken),
            CreatedAt = now,
            ExpiresAt = now.AddDays(jwtOptions.Value.RefreshTokenLifetimeDays)
        });
        await db.SaveChangesAsync(cancellationToken);

        // Čišćenje isteklih tokena ovog korisnika.
        await db.RefreshTokens.Where(x => x.UserId == user.Id && x.ExpiresAt < now)
            .ExecuteDeleteAsync(cancellationToken);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = new AuthUserDto
            {
                Id = user.Id,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                ProfileImageUrl = user.ProfileImageUrl
            }
        };
    }
}
