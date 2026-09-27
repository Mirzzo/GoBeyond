using GoBeyond.Core.DTOs.Reports;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Admin;
using GoBeyond.Infrastructure.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Reports;

public interface IReportService
{
    Task<MentorReportDto> GetMentorReportAsync(ReportSearchObject search, CancellationToken cancellationToken = default);
    Task<MentorReportDetailDto> GetMentorReportDetailAsync(int mentorProfileId, int? year, int? month, CancellationToken cancellationToken = default);
    Task<ClientReportDto> GetClientReportAsync(ReportSearchObject search, CancellationToken cancellationToken = default);
    Task<ClientReportDetailDto> GetClientReportDetailAsync(int clientProfileId, int? year, int? month, CancellationToken cancellationToken = default);
    Task<OverviewReportDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Poslovni izvještaji za administratora. "Mjesečna" polja (monthlyEarnings, timeOnPlatformMinutes,
/// completedTrainings) računaju se za odabrani mjesec (default: tekući); "total" polja su ukupna.
/// Zarada = uspješne (ne vraćene) uplate po datumu plaćanja. Vrijeme na platformi = zbir UserActivity (heartbeat).
/// </summary>
public sealed class ReportService(GoBeyondDbContext db, IPaymentGateway paymentGateway) : IReportService
{
    private const int BreakdownMonths = 6;

    public async Task<MentorReportDto> GetMentorReportAsync(ReportSearchObject search, CancellationToken cancellationToken = default)
    {
        var period = Period.Resolve(search.Year, search.Month);
        var query = ApprovedMentors();
        if (search.Search.NormalizeSearch() is { } term)
            query = query.Where(x => (x.User.FirstName + " " + x.User.LastName).Contains(term) ||
                                     (x.Nickname != null && x.Nickname.Contains(term)));
        if (search.TrainingTypeId is { } typeId) query = query.Where(x => x.TrainingTypeId == typeId);

        var rows = await ProjectMentorRows(query, period).ToListAsync(cancellationToken);
        var items = rows.Select(x => x.ToDto()).OrderByDescending(x => x.MonthlyEarnings).ThenBy(x => x.FullName).ToList();

        return new MentorReportDto
        {
            Items = items,
            Totals = new MentorReportTotalsDto
            {
                ActiveSubscribers = items.Sum(x => x.ActiveSubscribers),
                MonthlyEarnings = items.Sum(x => x.MonthlyEarnings),
                TotalEarnings = items.Sum(x => x.TotalEarnings),
                TimeOnPlatformMinutes = items.Sum(x => x.TimeOnPlatformMinutes),
                MentorCount = items.Count
            },
            Year = period.Year,
            Month = period.Month,
            Currency = paymentGateway.Currency
        };
    }

    public async Task<MentorReportDetailDto> GetMentorReportDetailAsync(int mentorProfileId, int? year, int? month,
        CancellationToken cancellationToken = default)
    {
        var period = Period.Resolve(year, month);
        var row = await ProjectMentorRows(db.MentorProfiles.AsNoTracking().Where(x => x.Id == mentorProfileId && !x.User.IsDeleted), period)
                      .FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException(DomainTexts.MentorNotFound);

        var first = period.AddMonths(-(BreakdownMonths - 1));
        var payments = await db.Payments.AsNoTracking()
            .Where(x => x.Subscription.MentorProfileId == mentorProfileId && x.Status == PaymentStatus.Succeeded &&
                        x.PaidAt >= first.Start && x.PaidAt < period.End)
            .Select(x => new { x.PaidAt, x.Amount }).ToListAsync(cancellationToken);
        var accepted = await db.Subscriptions.AsNoTracking()
            .Where(x => x.MentorProfileId == mentorProfileId && x.AcceptedAt >= first.Start && x.AcceptedAt < period.End)
            .Select(x => x.AcceptedAt).ToListAsync(cancellationToken);
        var activity = await ActivityByDayAsync(row.UserId, first, period, cancellationToken);

        var detail = new MentorReportDetailDto { Email = row.Email, Year = period.Year, Month = period.Month, Currency = paymentGateway.Currency };
        CopyMentorRow(row.ToDto(), detail);
        detail.MonthlyBreakdown = Enumerable.Range(0, BreakdownMonths).Select(i =>
        {
            var p = first.AddMonths(i);
            return new MentorMonthBreakdownDto
            {
                Year = p.Year,
                Month = p.Month,
                Earnings = payments.Where(x => p.Contains(x.PaidAt)).Sum(x => x.Amount),
                NewSubscribers = accepted.Count(p.Contains),
                MinutesOnPlatform = activity.Where(x => p.Contains(x.Day)).Sum(x => x.Seconds) / 60
            };
        }).ToList();
        return detail;
    }

    public async Task<ClientReportDto> GetClientReportAsync(ReportSearchObject search, CancellationToken cancellationToken = default)
    {
        var period = Period.Resolve(search.Year, search.Month);
        var query = db.ClientProfiles.AsNoTracking().Where(x => !x.User.IsDeleted && x.User.Role == UserRole.Client);
        if (search.Search.NormalizeSearch() is { } term)
            query = query.Where(x => (x.User.FirstName + " " + x.User.LastName).Contains(term) || x.User.Email.Contains(term));

        var rows = await ProjectClientRows(query, period).ToListAsync(cancellationToken);
        var items = rows.Select(x => x.ToDto()).OrderBy(x => x.FullName).ToList();

        return new ClientReportDto
        {
            Items = items,
            Totals = new ClientReportTotalsDto
            {
                ActiveSubscriptions = items.Sum(x => x.ActiveSubscriptions),
                TotalPaid = items.Sum(x => x.TotalPaid),
                CompletedTrainings = items.Sum(x => x.CompletedTrainings),
                ProgressEntries = items.Sum(x => x.ProgressEntries),
                TimeOnPlatformMinutes = items.Sum(x => x.TimeOnPlatformMinutes),
                ClientCount = items.Count
            },
            Year = period.Year,
            Month = period.Month,
            Currency = paymentGateway.Currency
        };
    }

    public async Task<ClientReportDetailDto> GetClientReportDetailAsync(int clientProfileId, int? year, int? month,
        CancellationToken cancellationToken = default)
    {
        var period = Period.Resolve(year, month);
        var row = await ProjectClientRows(db.ClientProfiles.AsNoTracking().Where(x => x.Id == clientProfileId && !x.User.IsDeleted), period)
                      .FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException("Klijent nije pronađen.");

        var first = period.AddMonths(-(BreakdownMonths - 1));
        var payments = await db.Payments.AsNoTracking()
            .Where(x => x.Subscription.ClientProfileId == clientProfileId && x.Status == PaymentStatus.Succeeded &&
                        x.PaidAt >= first.Start && x.PaidAt < period.End)
            .Select(x => new { x.PaidAt, x.Amount }).ToListAsync(cancellationToken);
        var sessions = await db.TrainingSessions.AsNoTracking()
            .Where(x => x.ClientProfileId == clientProfileId && x.CompletedAt >= first.Start && x.CompletedAt < period.End)
            .Select(x => x.CompletedAt).ToListAsync(cancellationToken);
        var activity = await ActivityByDayAsync(row.UserId, first, period, cancellationToken);

        var detail = new ClientReportDetailDto { Email = row.Email, Year = period.Year, Month = period.Month, Currency = paymentGateway.Currency };
        CopyClientRow(row.ToDto(), detail);
        detail.MonthlyBreakdown = Enumerable.Range(0, BreakdownMonths).Select(i =>
        {
            var p = first.AddMonths(i);
            return new ClientMonthBreakdownDto
            {
                Year = p.Year,
                Month = p.Month,
                Paid = payments.Where(x => p.Contains(x.PaidAt)).Sum(x => x.Amount),
                CompletedTrainings = sessions.Count(x => p.Contains(x)),
                MinutesOnPlatform = activity.Where(x => p.Contains(x.Day)).Sum(x => x.Seconds) / 60
            };
        }).ToList();
        return detail;
    }

    public async Task<OverviewReportDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var current = Period.Resolve(null, null);
        var first = current.AddMonths(-(BreakdownMonths - 1));

        var payments = await db.Payments.AsNoTracking()
            .Where(x => x.Status == PaymentStatus.Succeeded && x.PaidAt >= first.Start && x.PaidAt < current.End)
            .Select(x => new { x.PaidAt, x.Amount }).ToListAsync(cancellationToken);

        var topMentors = await ApprovedMentors().Where(x => x.User.IsActive)
            .Select(x => new
            {
                FullName = x.User.FirstName + " " + x.User.LastName,
                TrainingTypeName = x.TrainingType.Name,
                Average = x.Reviews.Average(r => (double?)r.Rating),
                ActiveSubscribers = x.Subscriptions.Count(s => s.Status == SubscriptionStatus.Active)
            })
            .OrderByDescending(x => x.ActiveSubscribers).ThenByDescending(x => x.Average)
            .Take(5)
            .ToListAsync(cancellationToken);

        return new OverviewReportDto
        {
            ClientCount = await db.Users.CountAsync(x => x.Role == UserRole.Client && !x.IsDeleted, cancellationToken),
            MentorCount = await ApprovedMentors().CountAsync(cancellationToken),
            PendingMentorRequests = await db.MentorProfiles.CountAsync(x => x.Status == MentorApprovalStatus.Pending && !x.User.IsDeleted, cancellationToken),
            ActiveSubscriptions = await db.Subscriptions.CountAsync(x => x.Status == SubscriptionStatus.Active, cancellationToken),
            MonthlyEarnings = payments.Where(x => current.Contains(x.PaidAt)).Sum(x => x.Amount),
            Currency = paymentGateway.Currency,
            EarningsLast6Months = Enumerable.Range(0, BreakdownMonths).Select(i =>
            {
                var p = first.AddMonths(i);
                return new MonthAmountDto { Year = p.Year, Month = p.Month, Amount = payments.Where(x => p.Contains(x.PaidAt)).Sum(x => x.Amount) };
            }).ToList(),
            TopMentors = topMentors.Select(x => new TopMentorDto
            {
                FullName = x.FullName,
                TrainingTypeName = x.TrainingTypeName,
                AverageRating = AdminMentorService.RoundRating(x.Average),
                ActiveSubscribers = x.ActiveSubscribers
            }).ToList()
        };
    }

    private IQueryable<MentorProfile> ApprovedMentors() =>
        db.MentorProfiles.AsNoTracking().Where(x => x.Status == MentorApprovalStatus.Approved && !x.User.IsDeleted);

    private static IQueryable<MentorRow> ProjectMentorRows(IQueryable<MentorProfile> query, Period period) =>
        query.Select(x => new MentorRow
        {
            MentorProfileId = x.Id,
            UserId = x.UserId,
            FullName = x.User.FirstName + " " + x.User.LastName,
            Email = x.User.Email,
            TrainingTypeName = x.TrainingType.Name,
            ActiveSubscribers = x.Subscriptions.Count(s => s.Status == SubscriptionStatus.Active),
            TotalSubscribers = x.Subscriptions.Where(s => s.AcceptedAt != null).Select(s => s.ClientProfileId).Distinct().Count(),
            MonthlyEarnings = x.Subscriptions.SelectMany(s => s.Payments)
                .Where(p => p.Status == PaymentStatus.Succeeded && p.PaidAt >= period.Start && p.PaidAt < period.End)
                .Sum(p => (decimal?)p.Amount) ?? 0,
            TotalEarnings = x.Subscriptions.SelectMany(s => s.Payments)
                .Where(p => p.Status == PaymentStatus.Succeeded).Sum(p => (decimal?)p.Amount) ?? 0,
            ActiveSeconds = x.User.Activities
                .Where(a => a.Day >= period.FirstDay && a.Day < period.EndDay).Sum(a => (int?)a.ActiveSeconds) ?? 0,
            AverageRating = x.Reviews.Average(r => (double?)r.Rating)
        });

    private static IQueryable<ClientRow> ProjectClientRows(IQueryable<ClientProfile> query, Period period) =>
        query.Select(x => new ClientRow
        {
            ClientProfileId = x.Id,
            UserId = x.UserId,
            FullName = x.User.FirstName + " " + x.User.LastName,
            Email = x.User.Email,
            ActiveMentorName = x.Subscriptions
                .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.AwaitingMentor)
                .Select(s => s.MentorProfile.User.FirstName + " " + s.MentorProfile.User.LastName).FirstOrDefault(),
            ActiveSubscriptions = x.Subscriptions.Count(s => s.Status == SubscriptionStatus.Active),
            TotalSubscriptions = x.Subscriptions.Count(s => s.Status != SubscriptionStatus.PendingPayment),
            TotalPaid = x.Subscriptions.SelectMany(s => s.Payments)
                .Where(p => p.Status == PaymentStatus.Succeeded).Sum(p => (decimal?)p.Amount) ?? 0,
            CompletedTrainings = x.TrainingSessions.Count(s => s.CompletedAt >= period.Start && s.CompletedAt < period.End),
            ProgressEntries = x.ProgressEntries.Count,
            LastProgressAt = x.ProgressEntries.Max(p => (DateTime?)p.UpdatedAt),
            ActiveSeconds = x.User.Activities
                .Where(a => a.Day >= period.FirstDay && a.Day < period.EndDay).Sum(a => (int?)a.ActiveSeconds) ?? 0
        });

    private async Task<List<(DateOnly Day, int Seconds)>> ActivityByDayAsync(int userId, Period first, Period last,
        CancellationToken cancellationToken)
    {
        var rows = await db.UserActivities.AsNoTracking()
            .Where(x => x.UserId == userId && x.Day >= first.FirstDay && x.Day < last.EndDay)
            .Select(x => new { x.Day, x.ActiveSeconds })
            .ToListAsync(cancellationToken);
        return rows.Select(x => (x.Day, x.ActiveSeconds)).ToList();
    }

    private static void CopyMentorRow(MentorReportRowDto source, MentorReportRowDto target)
    {
        target.MentorProfileId = source.MentorProfileId;
        target.FullName = source.FullName;
        target.TrainingTypeName = source.TrainingTypeName;
        target.ActiveSubscribers = source.ActiveSubscribers;
        target.TotalSubscribers = source.TotalSubscribers;
        target.MonthlyEarnings = source.MonthlyEarnings;
        target.TotalEarnings = source.TotalEarnings;
        target.TimeOnPlatformMinutes = source.TimeOnPlatformMinutes;
        target.AverageRating = source.AverageRating;
    }

    private static void CopyClientRow(ClientReportRowDto source, ClientReportRowDto target)
    {
        target.ClientProfileId = source.ClientProfileId;
        target.FullName = source.FullName;
        target.ActiveMentorName = source.ActiveMentorName;
        target.ActiveSubscriptions = source.ActiveSubscriptions;
        target.TotalSubscriptions = source.TotalSubscriptions;
        target.TotalPaid = source.TotalPaid;
        target.CompletedTrainings = source.CompletedTrainings;
        target.ProgressEntries = source.ProgressEntries;
        target.LastProgressAt = source.LastProgressAt;
        target.TimeOnPlatformMinutes = source.TimeOnPlatformMinutes;
    }

    private sealed class MentorRow
    {
        public int MentorProfileId { get; init; }
        public int UserId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string TrainingTypeName { get; init; } = string.Empty;
        public int ActiveSubscribers { get; init; }
        public int TotalSubscribers { get; init; }
        public decimal MonthlyEarnings { get; init; }
        public decimal TotalEarnings { get; init; }
        public int ActiveSeconds { get; init; }
        public double? AverageRating { get; init; }

        public MentorReportRowDto ToDto() => new()
        {
            MentorProfileId = MentorProfileId,
            FullName = FullName,
            TrainingTypeName = TrainingTypeName,
            ActiveSubscribers = ActiveSubscribers,
            TotalSubscribers = TotalSubscribers,
            MonthlyEarnings = MonthlyEarnings,
            TotalEarnings = TotalEarnings,
            TimeOnPlatformMinutes = ActiveSeconds / 60,
            AverageRating = AdminMentorService.RoundRating(AverageRating)
        };
    }

    private sealed class ClientRow
    {
        public int ClientProfileId { get; init; }
        public int UserId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? ActiveMentorName { get; init; }
        public int ActiveSubscriptions { get; init; }
        public int TotalSubscriptions { get; init; }
        public decimal TotalPaid { get; init; }
        public int CompletedTrainings { get; init; }
        public int ProgressEntries { get; init; }
        public DateTime? LastProgressAt { get; init; }
        public int ActiveSeconds { get; init; }

        public ClientReportRowDto ToDto() => new()
        {
            ClientProfileId = ClientProfileId,
            FullName = FullName,
            ActiveMentorName = ActiveMentorName,
            ActiveSubscriptions = ActiveSubscriptions,
            TotalSubscriptions = TotalSubscriptions,
            TotalPaid = TotalPaid,
            CompletedTrainings = CompletedTrainings,
            ProgressEntries = ProgressEntries,
            LastProgressAt = LastProgressAt,
            TimeOnPlatformMinutes = ActiveSeconds / 60
        };
    }

    /// <summary>Kalendarski mjesec [Start, End) u UTC.</summary>
    private readonly record struct Period(int Year, int Month)
    {
        public DateTime Start => new(Year, Month, 1, 0, 0, 0, DateTimeKind.Utc);
        public DateTime End => Start.AddMonths(1);
        public DateOnly FirstDay => new(Year, Month, 1);
        public DateOnly EndDay => FirstDay.AddMonths(1);

        public Period AddMonths(int months)
        {
            var date = Start.AddMonths(months);
            return new Period(date.Year, date.Month);
        }

        public bool Contains(DateTime? value) => value >= Start && value < End;
        public bool Contains(DateOnly value) => value >= FirstDay && value < EndDay;

        public static Period Resolve(int? year, int? month)
        {
            var now = DateTime.UtcNow;
            var errors = new ValidationErrorCollector();
            errors.Require(year is null or >= 2000 and <= 2100, "year", "Godina mora biti između 2000 i 2100.");
            errors.Require(month is null or >= 1 and <= 12, "month", "Mjesec mora biti broj od 1 do 12.");
            errors.ThrowIfAny();
            return new Period(year ?? now.Year, month ?? now.Month);
        }
    }
}
