namespace GoBeyond.Infrastructure.Common;

/// <summary>Nazivi dana i mjeseci na bosanskom (1 = Ponedjeljak, 1 = Januar).</summary>
public static class BosnianCalendar
{
    private static readonly string[] Days =
        ["Ponedjeljak", "Utorak", "Srijeda", "Četvrtak", "Petak", "Subota", "Nedjelja"];

    private static readonly string[] Months =
        ["Januar", "Februar", "Mart", "April", "Maj", "Juni", "Juli", "August", "Septembar", "Oktobar", "Novembar", "Decembar"];

    public static string DayName(int dayOfWeek) =>
        dayOfWeek is >= 1 and <= 7 ? Days[dayOfWeek - 1] : string.Empty;

    public static string MonthName(int month) =>
        month is >= 1 and <= 12 ? Months[month - 1] : string.Empty;

    /// <summary>Pretvara .NET DayOfWeek (nedjelja = 0) u 1..7 (ponedjeljak = 1).</summary>
    public static int ToPlanDay(DayOfWeek dayOfWeek) => dayOfWeek == DayOfWeek.Sunday ? 7 : (int)dayOfWeek;
}
