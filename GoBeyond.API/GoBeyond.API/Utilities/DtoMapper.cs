using GoBeyond.Core.DTOs;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;

namespace GoBeyond.API.Utilities;

internal static class DtoMapper
{
    public static string FullName(User user)
        => $"{user.FirstName} {user.LastName}".Trim();

    public static MentorCategory ParseMentorCategory(string value)
    {
        if (Enum.TryParse<MentorCategory>(value, ignoreCase: true, out var category))
        {
            return category;
        }

        throw new InvalidOperationException("Invalid mentor category.");
    }

    public static PaymentStatus ParsePaymentStatus(string value)
    {
        if (Enum.TryParse<PaymentStatus>(value, ignoreCase: true, out var status))
        {
            return status;
        }

        throw new InvalidOperationException("Invalid payment status.");
    }

    public static AdminMentorRequestDto ToAdminMentorRequest(MentorProfile mentor)
        => new(
            mentor.Id,
            mentor.UserId,
            FullName(mentor.User),
            mentor.User.Email,
            mentor.Category.ToString(),
            mentor.Price,
            mentor.Bio,
            mentor.Age,
            mentor.Status.ToString(),
            mentor.Certificates.Count,
            mentor.Certificates
                .OrderBy(x => x.Id)
                .Select(x => new FileReferenceDto(x.FileName, x.FileUrl))
                .ToList());

    public static AdminUserListItemDto ToAdminUserListItem(User user)
        => new(
            user.Id,
            user.Role switch
            {
                UserRole.Mentor => user.MentorProfile?.Id,
                UserRole.Client => user.ClientProfile?.Id,
                _ => null
            },
            FullName(user),
            user.Email,
            user.Role.ToString(),
            user.IsActive,
            user.MentorProfile?.Category.ToString(),
            user.ClientProfile?.FitnessLevel,
            user.Role switch
            {
                UserRole.Mentor => user.MentorProfile?.Subscriptions.Count(x => x.Status == SubscriptionStatus.Active) ?? 0,
                UserRole.Client => user.ClientProfile?.Subscriptions.Count(x => x.Status == SubscriptionStatus.Active) ?? 0,
                _ => 0
            },
            user.MentorProfile?.Price,
            user.MentorProfile?.Status.ToString() ?? (user.IsActive ? "Active" : "Blocked"),
            user.CreatedAt);

    public static AdminSubscriptionListItemDto ToAdminSubscriptionListItem(Subscription subscription)
        => new(
            subscription.Id,
            FullName(subscription.ClientProfile.User),
            subscription.ClientProfile.User.Email,
            FullName(subscription.MentorProfile.User),
            subscription.MentorProfile.User.Email,
            subscription.Status.ToString(),
            subscription.AmountPaid,
            subscription.StartDate,
            subscription.EndDate,
            subscription.Payments
                .OrderByDescending(x => x.Id)
                .Select(x => x.Status.ToString())
                .FirstOrDefault() ?? PaymentStatus.Pending.ToString(),
            subscription.Questionnaire?.PrimaryGoal ?? "No questionnaire",
            subscription.TrainingPlans.Any(x => x.Status == TrainingPlanStatus.Published));

    public static MentorReportDto ToMentorReport(MentorProfile mentor)
    {
        var activeSubscribers = mentor.Subscriptions.Count(x => x.Status == SubscriptionStatus.Active);
        var pendingRequests = mentor.Subscriptions.Count(x => x.Status == SubscriptionStatus.Active && !x.TrainingPlans.Any(y => y.Status == TrainingPlanStatus.Published));
        var publishedPlans = mentor.TrainingPlans.Count(x => x.Status == TrainingPlanStatus.Published);
        var totalRevenue = mentor.Subscriptions
            .Where(x => x.Status == SubscriptionStatus.Active || x.Status == SubscriptionStatus.Expired)
            .Sum(x => x.AmountPaid);
        var averageRating = mentor.Reviews.Count == 0
            ? 0
            : Math.Round(mentor.Reviews.Average(x => x.Rating), 1);

        return new MentorReportDto(
            mentor.Id,
            FullName(mentor.User),
            mentor.User.Email,
            mentor.Category.ToString(),
            mentor.Status.ToString(),
            mentor.Price,
            activeSubscribers,
            pendingRequests,
            publishedPlans,
            totalRevenue,
            averageRating,
            mentor.Reviews.Count,
            mentor.Subscriptions
                .OrderByDescending(x => x.StartDate)
                .Take(5)
                .Select(x => new MentorClientSnapshotDto(
                    FullName(x.ClientProfile.User),
                    x.Questionnaire?.PrimaryGoal ?? "General coaching",
                    x.Status.ToString(),
                    x.StartDate))
                .ToList());
    }

    public static MentorSummaryDto ToMentorSummary(MentorProfile mentor)
        => new(
            mentor.Id,
            mentor.UserId,
            FullName(mentor.User),
            mentor.User.Email,
            mentor.Category.ToString(),
            mentor.Price,
            mentor.Bio,
            mentor.Status.ToString(),
            AverageRating(mentor.Reviews),
            mentor.Reviews.Count,
            mentor.Subscriptions.Count(x => x.Status == SubscriptionStatus.Active),
            GetCityLabel(mentor.Category),
            GetHeadline(mentor.Bio),
            GetResponseTimeLabel(mentor.Subscriptions.Count(x => x.Status == SubscriptionStatus.Active)),
            GetNextStartLabel(mentor.Subscriptions.Count(x => x.Status == SubscriptionStatus.Active)),
            mentor.Reviews.Select(x => x.Comment).FirstOrDefault() ?? "Structured coaching with weekly feedback.",
            GetSpecialties(mentor.Category),
            mentor.User.ProfileImageUrl,
            GetAccentColor(mentor.Category));

    public static MentorDetailDto ToMentorDetail(MentorProfile mentor)
        => new(
            mentor.Id,
            mentor.UserId,
            FullName(mentor.User),
            mentor.User.Email,
            mentor.Category.ToString(),
            mentor.Price,
            mentor.Bio,
            mentor.Age,
            mentor.Status.ToString(),
            AverageRating(mentor.Reviews),
            mentor.Reviews.Count,
            mentor.Subscriptions.Count(x => x.Status == SubscriptionStatus.Active),
            GetCityLabel(mentor.Category),
            GetHeadline(mentor.Bio),
            GetResponseTimeLabel(mentor.Subscriptions.Count(x => x.Status == SubscriptionStatus.Active)),
            GetNextStartLabel(mentor.Subscriptions.Count(x => x.Status == SubscriptionStatus.Active)),
            mentor.Reviews.Select(x => x.Comment).FirstOrDefault() ?? "Structured coaching with weekly feedback.",
            GetSpecialties(mentor.Category),
            mentor.Certificates
                .OrderBy(x => x.Id)
                .Select(x => new FileReferenceDto(x.FileName, x.FileUrl))
                .ToList(),
            mentor.User.ProfileImageUrl);

    public static ReviewDto ToReviewDto(Review review)
        => new(
            review.Id,
            review.ClientProfile.UserId,
            FullName(review.ClientProfile.User),
            review.Rating,
            review.Comment);

    public static ClientQuestionnaireDto? ToQuestionnaireDto(Questionnaire? questionnaire)
        => questionnaire is null
            ? null
            : new ClientQuestionnaireDto(
                questionnaire.PrimaryGoal,
                questionnaire.TimeCommitment,
                questionnaire.HealthIssues,
                questionnaire.Medications,
                questionnaire.WeeklyAvailability,
                questionnaire.PhysicalActivityLevel);

    public static CollaborationRequestDto ToCollaborationRequest(Subscription subscription)
    {
        var questionnaire = ToQuestionnaireDto(subscription.Questionnaire)
            ?? throw new InvalidOperationException("Questionnaire is required.");

        return new CollaborationRequestDto(
            subscription.Id,
            subscription.ClientProfile.UserId,
            FullName(subscription.ClientProfile.User),
            subscription.ClientProfile.User.Email,
            subscription.ClientProfile.Age,
            subscription.ClientProfile.FitnessLevel,
            subscription.ClientProfile.Weight,
            subscription.ClientProfile.Height,
            subscription.StartDate,
            subscription.AmountPaid,
            questionnaire);
    }

    public static MentorSubscriberDto ToMentorSubscriber(Subscription subscription, ProgressEntry? latestProgress = null)
        => new(
            subscription.Id,
            subscription.ClientProfile.UserId,
            FullName(subscription.ClientProfile.User),
            subscription.ClientProfile.User.Email,
            subscription.Status.ToString(),
            subscription.StartDate,
            subscription.EndDate,
            subscription.TrainingPlans.Any(x => x.Status == TrainingPlanStatus.Published),
            subscription.TrainingPlans
                .OrderByDescending(x => x.Id)
                .Select(x => x.Status.ToString())
                .FirstOrDefault(),
            latestProgress is null ? null : $"{MonthName(latestProgress.Month)} {latestProgress.Year}",
            subscription.Questionnaire?.PrimaryGoal ?? "General coaching");

    public static ProgressEntryDto ToProgressEntryDto(ProgressEntry entry)
    {
        var subtitle = entry.Measurements
            ?? entry.Strength
            ?? entry.Conditioning
            ?? "Monthly check-in captured.";
        var metric = entry.Weight is not null
            ? $"{entry.Weight:0.##} kg"
            : entry.Strength ?? entry.Conditioning ?? "Updated";
        var positive = !string.IsNullOrWhiteSpace(entry.Strength) || !string.IsNullOrWhiteSpace(entry.Conditioning);

        return new ProgressEntryDto(
            entry.Id,
            entry.Year,
            entry.Month,
            entry.Weight,
            entry.Measurements,
            entry.Strength,
            entry.Conditioning,
            entry.PhotoUrl,
            $"{MonthName(entry.Month)} check-in",
            subtitle,
            metric,
            positive);
    }

    public static ProgressSummaryDto ToProgressSummary(IEnumerable<ProgressEntry> entries)
    {
        var orderedEntries = entries
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ToList();

        var latest = orderedEntries.FirstOrDefault();

        return new ProgressSummaryDto(
            latest?.Weight is null ? "No weight yet" : $"{latest.Weight:0.##} kg",
            $"{orderedEntries.Count} check-ins",
            latest?.Strength ?? "No strength notes",
            latest?.Conditioning ?? "No conditioning notes",
            latest is null ? null : new DateTime(latest.Year, latest.Month, 1));
    }

    public static ClientDetailDto ToClientDetail(Subscription subscription, IEnumerable<ProgressEntry> progressEntries)
    {
        var list = progressEntries
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .Take(6)
            .ToList();

        return new ClientDetailDto(
            subscription.ClientProfile.UserId,
            FullName(subscription.ClientProfile.User),
            subscription.ClientProfile.User.Email,
            subscription.ClientProfile.Age,
            subscription.ClientProfile.Weight,
            subscription.ClientProfile.Height,
            subscription.ClientProfile.FitnessLevel,
            subscription.Status.ToString(),
            subscription.StartDate,
            subscription.EndDate,
            subscription.AmountPaid,
            ToQuestionnaireDto(subscription.Questionnaire),
            ToProgressSummary(list),
            list.Select(ToProgressEntryDto).ToList());
    }

    public static PaymentDto ToPaymentDto(Payment payment)
        => new(
            payment.Id,
            payment.Status.ToString(),
            payment.Amount,
            payment.Currency,
            payment.StripePaymentIntentId);

    public static SubscriptionDto ToSubscriptionDto(Subscription subscription)
    {
        var plan = subscription.TrainingPlans
            .OrderByDescending(x => x.Status)
            .ThenByDescending(x => x.WeekNumber)
            .FirstOrDefault();
        var paymentStatus = subscription.Payments
            .OrderByDescending(x => x.Id)
            .Select(x => x.Status.ToString())
            .FirstOrDefault() ?? PaymentStatus.Pending.ToString();
        var durationWeeks = Math.Max(1, (int)Math.Ceiling((subscription.EndDate - subscription.StartDate).TotalDays / 7));

        return new SubscriptionDto(
            subscription.Id,
            subscription.MentorProfileId,
            FullName(subscription.MentorProfile.User),
            subscription.MentorProfile.User.Email,
            subscription.Status.ToString(),
            subscription.StartDate,
            subscription.EndDate,
            subscription.AmountPaid,
            paymentStatus,
            $"{subscription.MentorProfile.Category} Coaching / {durationWeeks} Weeks",
            $"Renews {subscription.EndDate:dd MMM}",
            $"{subscription.StartDate.DayOfWeek} check-in",
            plan is null ? "Waiting for week 1" : $"Week {plan.WeekNumber}",
            subscription.Questionnaire?.PrimaryGoal,
            subscription.TrainingPlans.Any(x => x.Status == TrainingPlanStatus.Published));
    }

    public static SubscriptionDetailDto ToSubscriptionDetailDto(Subscription subscription)
        => new(
            ToSubscriptionDto(subscription),
            ToQuestionnaireDto(subscription.Questionnaire),
            subscription.Payments
                .OrderByDescending(x => x.Id)
                .Select(ToPaymentDto)
                .ToList());

    public static TrainingPlanSummaryDto ToTrainingPlanSummary(TrainingPlan trainingPlan)
    {
        var orderedDays = trainingPlan.DayPlans
            .OrderBy(x => DayOfWeekOrder(x.DayOfWeek))
            .ToList();
        var dayCount = orderedDays.Count;
        var focusTitle = orderedDays.FirstOrDefault()?.TrainingDescription ?? "Structured training week";
        var focusSummary = string.Join(
            " | ",
            orderedDays
                .Take(2)
                .Select(x => $"{x.DayOfWeek}: {TrimForSummary(x.TrainingDescription, 44)}"));
        var nextSession = orderedDays.FirstOrDefault();
        var completedSessions = orderedDays.Count(x => DayOfWeekOrder(x.DayOfWeek) < DayOfWeekOrder(DateTime.UtcNow.DayOfWeek));

        return new TrainingPlanSummaryDto(
            trainingPlan.Id,
            trainingPlan.SubscriptionId,
            trainingPlan.ClientProfile.UserId,
            FullName(trainingPlan.ClientProfile.User),
            FullName(trainingPlan.MentorProfile.User),
            trainingPlan.WeekNumber,
            trainingPlan.Status.ToString(),
            trainingPlan.MotivationalQuote,
            dayCount,
            focusTitle,
            string.IsNullOrWhiteSpace(focusSummary) ? "Daily structure ready for review." : focusSummary,
            nextSession?.TrainingDescription ?? "No session yet",
            nextSession is null ? "-" : FormatDuration(nextSession.TrainingDuration),
            Math.Min(completedSessions, dayCount),
            dayCount);
    }

    public static TrainingPlanDetailDto ToTrainingPlanDetail(TrainingPlan trainingPlan)
    {
        var summary = ToTrainingPlanSummary(trainingPlan);
        return new TrainingPlanDetailDto(
            summary.Id,
            summary.SubscriptionId,
            summary.ClientUserId,
            summary.ClientName,
            summary.WeekNumber,
            summary.Status,
            summary.MotivationalQuote,
            summary.FocusTitle,
            summary.FocusSummary,
            summary.NextSessionTitle,
            summary.NextSessionDuration,
            summary.CompletedSessions,
            summary.TotalSessions,
            trainingPlan.DayPlans
                .OrderBy(x => DayOfWeekOrder(x.DayOfWeek))
                .Select(x => new DayPlanDto(
                    x.Id,
                    x.DayOfWeek.ToString(),
                    FormatDuration(x.TrainingDuration),
                    x.TrainingDescription,
                    FormatDuration(x.NutritionDuration),
                    x.NutritionDescription))
                .ToList());
    }

    public static NotificationDto ToNotificationDto(Notification notification)
        => new(
            notification.Id,
            notification.Title,
            notification.Body,
            notification.Type.ToString(),
            notification.IsRead);

    public static int GetAccentColor(MentorCategory category)
        => category switch
        {
            MentorCategory.Hybrid => unchecked((int)0xFFF2A541),
            MentorCategory.Calisthenics => unchecked((int)0xFF5DD6C0),
            MentorCategory.Weightlifting => unchecked((int)0xFF8FA8FF),
            _ => unchecked((int)0xFFF2A541)
        };

    private static double AverageRating(IEnumerable<Review> reviews)
        => reviews.Any() ? Math.Round(reviews.Average(x => x.Rating), 1) : 0;

    private static IReadOnlyList<string> GetSpecialties(MentorCategory category)
        => category switch
        {
            MentorCategory.Hybrid => ["Strength base", "Recovery pacing", "Weekly check-ins"],
            MentorCategory.Calisthenics => ["Bodyweight progressions", "Mobility", "Technique reviews"],
            MentorCategory.Weightlifting => ["Snatch timing", "Clean & jerk", "Volume tolerance"],
            _ => ["Weekly coaching"]
        };

    private static string GetCityLabel(MentorCategory category)
        => category switch
        {
            MentorCategory.Hybrid => "Sarajevo",
            MentorCategory.Calisthenics => "Mostar",
            MentorCategory.Weightlifting => "Zagreb",
            _ => "Remote"
        };

    private static string GetHeadline(string bio)
        => TrimForSummary(string.IsNullOrWhiteSpace(bio) ? "Structured coaching with weekly reviews." : bio, 82);

    private static string GetResponseTimeLabel(int activeClients)
        => activeClients switch
        {
            <= 8 => "< 3h response",
            <= 18 => "Same-day feedback",
            _ => "24h feedback window"
        };

    private static string GetNextStartLabel(int activeClients)
        => activeClients switch
        {
            <= 6 => "Starts Monday",
            <= 12 => "2 spots left",
            _ => "Assessment open"
        };

    private static int DayOfWeekOrder(DayOfWeek dayOfWeek)
        => dayOfWeek switch
        {
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
            DayOfWeek.Sunday => 7,
            _ => 8
        };

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{Math.Round(duration.TotalMinutes)} min";
        }

        return $"{Math.Round(duration.TotalMinutes)} min";
    }

    private static string TrimForSummary(string value, int length)
    {
        if (value.Length <= length)
        {
            return value;
        }

        return $"{value[..length].TrimEnd()}...";
    }

    private static string MonthName(int month)
        => new DateTime(2000, month, 1).ToString("MMM");
}
