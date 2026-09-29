import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/utils/date_of_birth_range.dart';

void main() {
  test('matches the backend age rule: 16 years ago is the latest date, the '
      'day after 101 years ago the earliest', () {
    final range = DateOfBirthRange(DateTime.utc(2026, 9, 29, 12));
    // Age 100 on 29.09.2026 (still valid) / age 101 (rejected).
    expect(range.first, DateTime(1925, 9, 30));
    // Exactly 16 today.
    expect(range.last, DateTime(2010, 9, 29));
  });

  test('uses the UTC date, like the backend', () {
    // 30.09. 01:30 in UTC+2 is still 29.09. in UTC.
    final range = DateOfBirthRange(DateTime.utc(2026, 9, 29, 23, 30));
    expect(range.last, DateTime(2010, 9, 29));
  });

  // DateTime.now() is a local time. Around midnight its date differs from
  // the UTC date the backend uses, east of UTC (already the next day) as
  // well as west of it (still the previous day). Needs a non-UTC local time
  // zone to tell the two apart.
  final midnightUtc = DateTime.utc(2026, 9, 30);
  final offset = midnightUtc.toLocal().timeZoneOffset;
  test(
    'a local "now" is converted to the UTC date first',
    () {
      final DateTime localNow;
      final DateTime expectedLast;
      if (offset > Duration.zero) {
        // Local time is already 30.09., UTC is still 29.09.
        localNow =
            midnightUtc.subtract(const Duration(minutes: 15)).toLocal();
        expectedLast = DateTime(2010, 9, 29);
      } else {
        // Local time is still 29.09., UTC is already 30.09.
        localNow = midnightUtc.add(const Duration(minutes: 15)).toLocal();
        expectedLast = DateTime(2010, 9, 30);
      }
      expect(localNow.isUtc, isFalse);
      expect(localNow.day, isNot(localNow.toUtc().day));

      final range = DateOfBirthRange(localNow);
      expect(range.last, expectedLast);
      expect(
        range.first,
        DateTime(1925, expectedLast.month, expectedLast.day + 1),
      );
    },
    skip: offset == Duration.zero,
  );

  test('a 29 February "today" maps like .NET AddYears (to 28 February)', () {
    final range = DateOfBirthRange(DateTime.utc(2028, 2, 29, 12));
    expect(range.last, DateTime(2012, 2, 29)); // 2012 is a leap year.
    expect(range.first, DateTime(1927, 3, 1)); // 1927-02-28 + 1 day.
  });

  test('clamp keeps an in-range date and moves others to the nearest end', () {
    final range = DateOfBirthRange(DateTime.utc(2026, 9, 29, 12));
    expect(range.clamp(DateTime(1990, 5, 1)), DateTime(1990, 5, 1));
    expect(range.clamp(DateTime(1920, 1, 1)), range.first);
    expect(range.clamp(DateTime(2015, 1, 1)), range.last);
  });
}
