using GoBeyond.API.Extensions;
using GoBeyond.API.Utilities;
using GoBeyond.Core.DTOs;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Utilities;
using GoBeyond.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin")]
public class AdminController(GoBeyondDbContext dbContext, IPasswordHasherService passwordHasherService, INotificationPublisher notificationPublisher) : ControllerBase
{
    [HttpPut("users/{id:int}")]
    public async Task<AdminUserListItemDto> UpdateUser(int id, [FromBody] UpdateAdminUserRequestDto request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(x => x.MentorProfile).ThenInclude(x => x!.Subscriptions)
            .Include(x => x.ClientProfile).ThenInclude(x => x!.Subscriptions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || !Enum.IsDefined(role))
            throw new InvalidOperationException("Role must be Admin, Mentor or Client.");
        if (id == User.GetUserId() && role != UserRole.Admin)
            throw new InvalidOperationException("You cannot remove your own administrator role.");
        if (user.Role == UserRole.Admin && role != UserRole.Admin)
            throw new InvalidOperationException("Administrator role cannot be removed from another administrator.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(x => x.Id != id && x.Email == email, cancellationToken))
            throw new InvalidOperationException("Email is already registered.");

        if (role == UserRole.Mentor && user.MentorProfile is null)
        {
            if (string.IsNullOrWhiteSpace(request.Bio) || request.Bio.Trim().Length < 10 ||
                request.Age is null or < 18 or > 80 || request.Price is null or <= 0 ||
                !Enum.TryParse<MentorCategory>(request.Category, true, out var category) || !Enum.IsDefined(category) ||
                string.IsNullOrWhiteSpace(request.CertificateFileUrl))
                throw new InvalidOperationException("Changing to Mentor requires bio (10+ characters), age (18-80), category, positive price and certificate URL.");
            user.MentorProfile = new MentorProfile
            {
                Bio = request.Bio!.Trim(), Age = request.Age!.Value, Category = category,
                Price = request.Price.Value, Status = MentorApprovalStatus.Pending,
                Certificates = [new MentorCertificate { FileName = request.CertificateFileName?.Trim() ?? "Certificate", FileUrl = request.CertificateFileUrl.Trim() }]
            };
            user.MentorProfile.TrainingTypeId = await dbContext.TrainingTypes
                .Where(x => x.Name == category.ToString()).Select(x => (int?)x.Id).FirstOrDefaultAsync(cancellationToken);
        }
        if (role == UserRole.Client && user.ClientProfile is null)
        {
            if (request.Age is null or < 18 or > 100 || request.Weight is null or <= 0 ||
                request.Height is null or <= 0 || string.IsNullOrWhiteSpace(request.FitnessLevel) ||
                !new[] { "Male", "Female", "Other" }.Contains(request.Sex, StringComparer.OrdinalIgnoreCase) ||
                !new[] { "Beginner", "Intermediate", "Advanced" }.Contains(request.TrainingExperience, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Changing to Client requires age 18-100, positive weight/height, fitness level, sex and training experience.");
            user.ClientProfile = new ClientProfile
            {
                Age = request.Age!.Value, Weight = request.Weight.Value,
                Height = request.Height.Value, FitnessLevel = request.FitnessLevel!.Trim(),
                Sex = request.Sex!, TrainingExperience = request.TrainingExperience!
            };
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = email;
        if (user.Role != role)
        {
            user.Role = role;
            await RevokeTokensAsync(id, cancellationToken);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return DtoMapper.ToAdminUserListItem(user);
    }

    [HttpPut("users/{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetUserPasswordRequestDto request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");
        user.PasswordHash = passwordHasherService.Hash(request.NewPassword);
        await RevokeTokensAsync(id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("users/{id:int}/unblock")]
    public async Task<AdminUserListItemDto> UnblockUser(int id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(x => x.MentorProfile).ThenInclude(x => x!.Subscriptions)
            .Include(x => x.ClientProfile).ThenInclude(x => x!.Subscriptions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");
        user.IsActive = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        return DtoMapper.ToAdminUserListItem(user);
    }

    private async Task RevokeTokensAsync(int userId, CancellationToken cancellationToken)
    {
        var tokens = await dbContext.RefreshTokens.Where(x => x.UserId == userId && !x.IsRevoked).ToListAsync(cancellationToken);
        foreach (var token in tokens) token.IsRevoked = true;
    }

    [HttpGet("mentors/{id:int}/certificates")]
    public async Task<IReadOnlyList<FileReferenceDto>> GetMentorCertificates(int id, CancellationToken cancellationToken)
    {
        var mentor = await dbContext.MentorProfiles.Include(x => x.Certificates)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Mentor not found.");
        return mentor.Certificates.OrderBy(x => x.Id)
            .Select(x => new FileReferenceDto(x.FileName, x.FileUrl)).ToList();
    }

    [HttpPut("mentors/{id:int}/certificates/{certificateId:int}")]
    public async Task<FileReferenceDto> UpdateMentorCertificate(int id, int certificateId, [FromBody] UploadCertificateRequestDto request, CancellationToken cancellationToken)
    {
        var certificate = await dbContext.MentorCertificates.FirstOrDefaultAsync(
            x => x.Id == certificateId && x.MentorProfileId == id, cancellationToken)
            ?? throw new InvalidOperationException("Certificate not found for mentor.");
        certificate.FileName = request.FileName.Trim();
        certificate.FileUrl = request.FileUrl.Trim();
        var mentor = await dbContext.MentorProfiles.FindAsync([id], cancellationToken);
        if (mentor is not null) mentor.Status = MentorApprovalStatus.Pending;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new FileReferenceDto(certificate.FileName, certificate.FileUrl);
    }

    [HttpDelete("mentors/{id:int}/certificates/{certificateId:int}")]
    public async Task<IActionResult> DeleteMentorCertificate(int id, int certificateId, CancellationToken cancellationToken)
    {
        var certificate = await dbContext.MentorCertificates.FirstOrDefaultAsync(
            x => x.Id == certificateId && x.MentorProfileId == id, cancellationToken)
            ?? throw new InvalidOperationException("Certificate not found for mentor.");
        var count = await dbContext.MentorCertificates.CountAsync(x => x.MentorProfileId == id, cancellationToken);
        if (count <= 1) throw new InvalidOperationException("Mentor must retain at least one certificate.");
        dbContext.MentorCertificates.Remove(certificate);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("mentor-requests")]
    public async Task<IReadOnlyList<AdminMentorRequestDto>> GetMentorRequests(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.MentorProfiles
            .Include(x => x.User)
            .Include(x => x.Certificates)
            .Where(x => x.Status == MentorApprovalStatus.Pending);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            var hasCategoryFilter = Enum.TryParse<MentorCategory>(search, ignoreCase: true, out var parsedCategory);
            query = query.Where(x =>
                x.User.FirstName.ToLower().Contains(normalizedSearch) ||
                x.User.LastName.ToLower().Contains(normalizedSearch) ||
                x.User.Email.ToLower().Contains(normalizedSearch) ||
                x.Bio.ToLower().Contains(normalizedSearch) ||
                (hasCategoryFilter && x.Category == parsedCategory));
        }

        var mentorRequests = await query
            .OrderBy(x => x.User.FirstName)
            .ThenBy(x => x.User.LastName)
            .ToListAsync(cancellationToken);

        return mentorRequests
            .Select(DtoMapper.ToAdminMentorRequest)
            .ToList();
    }

    [HttpPut("mentor-requests/{id:int}/approve")]
    public async Task<AdminMentorRequestDto> ApproveMentorRequest(int id, CancellationToken cancellationToken)
    {
        var mentor = await dbContext.MentorProfiles
            .Include(x => x.User)
            .Include(x => x.Certificates)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Mentor request not found.");

        mentor.Status = MentorApprovalStatus.Approved;

        dbContext.Notifications.Add(new Notification
        {
            UserId = mentor.UserId,
            Title = "Mentor account approved",
            Body = "Your mentor account has been approved. You can now sign in to the desktop panel.",
            Type = NotificationType.MentorApproved,
            IsRead = false
        });

        await notificationPublisher.PublishAsync("mentor.approved", mentor.User.Email,
            "Mentor account approved", "Your mentor account has been approved.", cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return DtoMapper.ToAdminMentorRequest(mentor);
    }

    [HttpPut("mentor-requests/{id:int}/reject")]
    public async Task<AdminMentorRequestDto> RejectMentorRequest(int id, CancellationToken cancellationToken)
    {
        var mentor = await dbContext.MentorProfiles
            .Include(x => x.User)
            .Include(x => x.Certificates)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Mentor request not found.");

        mentor.Status = MentorApprovalStatus.Rejected;

        dbContext.Notifications.Add(new Notification
        {
            UserId = mentor.UserId,
            Title = "Mentor account rejected",
            Body = "Your mentor request was rejected. Update your profile details and certificate before retrying.",
            Type = NotificationType.MentorApproved,
            IsRead = false
        });

        await notificationPublisher.PublishAsync("mentor.rejected", mentor.User.Email,
            "Mentor account rejected", "Your mentor request was rejected. Please update your profile and certificate.", cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return DtoMapper.ToAdminMentorRequest(mentor);
    }

    [HttpGet("mentors")]
    public async Task<IReadOnlyList<AdminUserListItemDto>> GetMentors(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Users
            .Include(x => x.MentorProfile)
                .ThenInclude(x => x!.Subscriptions)
            .Where(x => x.Role == UserRole.Mentor && x.MentorProfile != null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            var hasCategoryFilter = Enum.TryParse<MentorCategory>(search, ignoreCase: true, out var parsedCategory);
            query = query.Where(x =>
                x.FirstName.ToLower().Contains(normalizedSearch) ||
                x.LastName.ToLower().Contains(normalizedSearch) ||
                x.Email.ToLower().Contains(normalizedSearch) ||
                x.MentorProfile!.Bio.ToLower().Contains(normalizedSearch) ||
                (hasCategoryFilter && x.MentorProfile.Category == parsedCategory));
        }

        var mentors = await query
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .ToListAsync(cancellationToken);

        return mentors
            .Select(DtoMapper.ToAdminUserListItem)
            .ToList();
    }

    [HttpGet("clients")]
    public async Task<IReadOnlyList<AdminUserListItemDto>> GetClients(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Users
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x!.Subscriptions)
            .Where(x => x.Role == UserRole.Client && x.ClientProfile != null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.FirstName.ToLower().Contains(normalizedSearch) ||
                x.LastName.ToLower().Contains(normalizedSearch) ||
                x.Email.ToLower().Contains(normalizedSearch) ||
                x.ClientProfile!.FitnessLevel.ToLower().Contains(normalizedSearch));
        }

        var clients = await query
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .ToListAsync(cancellationToken);

        return clients
            .Select(DtoMapper.ToAdminUserListItem)
            .ToList();
    }

    [HttpPut("users/{id:int}/block")]
    public async Task<AdminUserListItemDto> BlockUser(int id, CancellationToken cancellationToken)
    {
        var currentUserId = User.GetUserId();
        if (id == currentUserId)
        {
            throw new InvalidOperationException("You cannot block your own account.");
        }

        var user = await dbContext.Users
            .Include(x => x.MentorProfile)
                .ThenInclude(x => x!.Subscriptions)
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x!.Subscriptions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (user.Role == UserRole.Admin)
        {
            throw new InvalidOperationException("Admin accounts cannot be blocked.");
        }

        user.IsActive = false;

        if (user.MentorProfile is not null)
            foreach (var subscription in user.MentorProfile.Subscriptions.Where(x => x.Status == SubscriptionStatus.Active))
                subscription.Status = SubscriptionStatus.Cancelled;

        var refreshTokens = await dbContext.RefreshTokens
            .Where(x => x.UserId == user.Id && !x.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in refreshTokens)
        {
            refreshToken.IsRevoked = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return DtoMapper.ToAdminUserListItem(user);
    }

    [HttpDelete("users/{id:int}")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        var currentUserId = User.GetUserId();
        if (id == currentUserId)
        {
            throw new InvalidOperationException("You cannot delete your own account.");
        }

        var user = await dbContext.Users
            .Include(x => x.MentorProfile).ThenInclude(x => x!.Subscriptions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (user.Role == UserRole.Admin)
        {
            throw new InvalidOperationException("Admin accounts cannot be deleted.");
        }

        user.IsActive = false;

        if (user.MentorProfile is not null)
            foreach (var subscription in user.MentorProfile.Subscriptions.Where(x => x.Status == SubscriptionStatus.Active))
                subscription.Status = SubscriptionStatus.Cancelled;

        var refreshTokens = await dbContext.RefreshTokens
            .Where(x => x.UserId == user.Id && !x.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in refreshTokens)
        {
            refreshToken.IsRevoked = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("reports/mentors/{id:int}")]
    public async Task<MentorReportDto> GetMentorReport(int id, CancellationToken cancellationToken)
    {
        var mentor = await dbContext.MentorProfiles
            .Include(x => x.User)
            .Include(x => x.Subscriptions)
                .ThenInclude(x => x.ClientProfile)
                    .ThenInclude(x => x.User)
            .Include(x => x.TrainingPlans)
            .Include(x => x.Reviews)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Mentor not found.");

        return DtoMapper.ToMentorReport(mentor);
    }

    [HttpGet("reports/overview")]
    public async Task<AdminOverviewReportDto> GetOverviewReport(CancellationToken cancellationToken)
    {
        var users = await dbContext.Users.AsNoTracking().ToListAsync(cancellationToken);
        var mentorProfiles = await dbContext.MentorProfiles
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var subscriptions = await dbContext.Subscriptions
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var trainingPlans = await dbContext.TrainingPlans
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var totalRevenue = subscriptions
            .Where(x => x.Status == SubscriptionStatus.Active || x.Status == SubscriptionStatus.Expired)
            .Sum(x => x.AmountPaid);
        var activeSubscriptions = subscriptions.Count(x => x.Status == SubscriptionStatus.Active);
        var pendingMentors = mentorProfiles.Count(x => x.Status == MentorApprovalStatus.Pending);
        var publishedPlans = trainingPlans.Count(x => x.Status == TrainingPlanStatus.Published);

        var monthlyClients = users
            .Where(x => x.Role == UserRole.Client)
            .GroupBy(x => new { x.CreatedAt.Year, x.CreatedAt.Month })
            .OrderBy(x => x.Key.Year)
            .ThenBy(x => x.Key.Month)
            .TakeLast(6)
            .Select(x => new OverviewTrendPointDto(
                new DateTime(x.Key.Year, x.Key.Month, 1).ToString("MMM"),
                x.Count()))
            .ToList();

        var monthlyRevenue = subscriptions
            .GroupBy(x => new { x.StartDate.Year, x.StartDate.Month })
            .OrderBy(x => x.Key.Year)
            .ThenBy(x => x.Key.Month)
            .TakeLast(6)
            .Select(x => new OverviewTrendPointDto(
                new DateTime(x.Key.Year, x.Key.Month, 1).ToString("MMM"),
                (int)Math.Round(x.Sum(y => y.AmountPaid))))
            .ToList();

        var alerts = new List<string>();
        if (pendingMentors > 0)
        {
            alerts.Add($"{pendingMentors} mentor request(s) waiting for review.");
        }

        if (activeSubscriptions == 0)
        {
            alerts.Add("No active subscriptions are currently seeded.");
        }

        if (publishedPlans == 0)
        {
            alerts.Add("No published training plans exist yet.");
        }

        return new AdminOverviewReportDto(
            [
                new OverviewMetricDto("Active subscriptions", activeSubscriptions.ToString(), $"{subscriptions.Count} total"),
                new OverviewMetricDto("Revenue", $"{totalRevenue:0.##} BAM", "Lifetime seeded value"),
                new OverviewMetricDto("Pending mentors", pendingMentors.ToString(), "Needs admin action"),
                new OverviewMetricDto("Published plans", publishedPlans.ToString(), "Client-ready plans")
            ],
            monthlyClients,
            monthlyRevenue,
            alerts);
    }

    [HttpGet("subscriptions")]
    public async Task<IReadOnlyList<AdminSubscriptionListItemDto>> GetSubscriptions(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Subscriptions
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.MentorProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.Questionnaire)
            .Include(x => x.Payments)
            .Include(x => x.TrainingPlans)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            var hasStatusFilter = Enum.TryParse<SubscriptionStatus>(search, ignoreCase: true, out var parsedStatus);
            query = query.Where(x =>
                x.ClientProfile.User.FirstName.ToLower().Contains(normalizedSearch) ||
                x.ClientProfile.User.LastName.ToLower().Contains(normalizedSearch) ||
                x.ClientProfile.User.Email.ToLower().Contains(normalizedSearch) ||
                x.MentorProfile.User.FirstName.ToLower().Contains(normalizedSearch) ||
                x.MentorProfile.User.LastName.ToLower().Contains(normalizedSearch) ||
                x.MentorProfile.User.Email.ToLower().Contains(normalizedSearch) ||
                (hasStatusFilter && x.Status == parsedStatus) ||
                (x.Questionnaire != null && x.Questionnaire.PrimaryGoal.ToLower().Contains(normalizedSearch)));
        }

        var subscriptions = await query
            .OrderByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);

        return subscriptions
            .Select(DtoMapper.ToAdminSubscriptionListItem)
            .ToList();
    }
}
