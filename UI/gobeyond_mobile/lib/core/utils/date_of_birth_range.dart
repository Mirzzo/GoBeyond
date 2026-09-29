/// The dates of birth the backend accepts. `[DateOfBirth(16, 100)]` counts
/// the age in whole years against the current UTC date, so the latest valid
/// date is exactly 16 years ago and the earliest is the day after the date
/// exactly 101 years ago.
class DateOfBirthRange {
  factory DateOfBirthRange(DateTime now) {
    final utc = now.toUtc();
    final today = DateTime(utc.year, utc.month, utc.day);
    final oldest = _yearsBefore(today, 101);
    return DateOfBirthRange._(
      DateTime(oldest.year, oldest.month, oldest.day + 1),
      _yearsBefore(today, 16),
    );
  }

  const DateOfBirthRange._(this.first, this.last);

  final DateTime first;
  final DateTime last;

  /// [date] moved into [first]..[last], e.g. a saved date of birth that has
  /// since fallen out of the range, so it is a valid initial picker date.
  DateTime clamp(DateTime date) {
    if (date.isBefore(first)) return first;
    if (date.isAfter(last)) return last;
    return date;
  }

  // Like .NET's DateOnly.AddYears: 29 February becomes 28 February in a
  // year that is not a leap year.
  static DateTime _yearsBefore(DateTime date, int years) {
    final year = date.year - years;
    final isLeap = (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;
    final day = date.month == 2 && date.day == 29 && !isLeap ? 28 : date.day;
    return DateTime(year, date.month, day);
  }
}
