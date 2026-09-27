namespace GoBeyond.Core.DTOs.Reports;

public class MentorReportRowDto
{
    public int MentorProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string TrainingTypeName { get; set; } = string.Empty;
    public int ActiveSubscribers { get; set; }
    public int TotalSubscribers { get; set; }
    public decimal MonthlyEarnings { get; set; }
    public decimal TotalEarnings { get; set; }
    public int TimeOnPlatformMinutes { get; set; }
    public decimal AverageRating { get; set; }
}

public sealed class MentorReportTotalsDto
{
    public int ActiveSubscribers { get; set; }
    public decimal MonthlyEarnings { get; set; }
    public decimal TotalEarnings { get; set; }
    public int TimeOnPlatformMinutes { get; set; }
    public int MentorCount { get; set; }
}

public sealed class MentorReportDto
{
    public List<MentorReportRowDto> Items { get; set; } = [];
    public MentorReportTotalsDto Totals { get; set; } = new();
    public int Year { get; set; }
    public int Month { get; set; }
    public string Currency { get; set; } = string.Empty;
}

public sealed class MentorMonthBreakdownDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Earnings { get; set; }
    public int NewSubscribers { get; set; }
    public int MinutesOnPlatform { get; set; }
}

public sealed class MentorReportDetailDto : MentorReportRowDto
{
    public string Email { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<MentorMonthBreakdownDto> MonthlyBreakdown { get; set; } = [];
}

public class ClientReportRowDto
{
    public int ClientProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? ActiveMentorName { get; set; }
    public int ActiveSubscriptions { get; set; }
    public int TotalSubscriptions { get; set; }
    public decimal TotalPaid { get; set; }
    public int CompletedTrainings { get; set; }
    public int ProgressEntries { get; set; }
    public DateTime? LastProgressAt { get; set; }
    public int TimeOnPlatformMinutes { get; set; }
}

public sealed class ClientReportTotalsDto
{
    public int ActiveSubscriptions { get; set; }
    public decimal TotalPaid { get; set; }
    public int CompletedTrainings { get; set; }
    public int ProgressEntries { get; set; }
    public int TimeOnPlatformMinutes { get; set; }
    public int ClientCount { get; set; }
}

public sealed class ClientReportDto
{
    public List<ClientReportRowDto> Items { get; set; } = [];
    public ClientReportTotalsDto Totals { get; set; } = new();
    public int Year { get; set; }
    public int Month { get; set; }
    public string Currency { get; set; } = string.Empty;
}

public sealed class ClientMonthBreakdownDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Paid { get; set; }
    public int CompletedTrainings { get; set; }
    public int MinutesOnPlatform { get; set; }
}

public sealed class ClientReportDetailDto : ClientReportRowDto
{
    public string Email { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<ClientMonthBreakdownDto> MonthlyBreakdown { get; set; } = [];
}

public sealed class MonthAmountDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
}

public sealed class TopMentorDto
{
    public string FullName { get; set; } = string.Empty;
    public string TrainingTypeName { get; set; } = string.Empty;
    public decimal AverageRating { get; set; }
    public int ActiveSubscribers { get; set; }
}

public sealed class OverviewReportDto
{
    public int ClientCount { get; set; }
    public int MentorCount { get; set; }
    public int PendingMentorRequests { get; set; }
    public int ActiveSubscriptions { get; set; }
    public decimal MonthlyEarnings { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<MonthAmountDto> EarningsLast6Months { get; set; } = [];
    public List<TopMentorDto> TopMentors { get; set; } = [];
}
