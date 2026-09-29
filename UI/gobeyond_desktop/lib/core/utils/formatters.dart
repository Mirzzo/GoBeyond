/// Small formatting helpers shared across screens/reports.
class Formatters {
  const Formatters._();

  static const _monthNames = [
    'Januar', 'Februar', 'Mart', 'April', 'Maj', 'Juni',
    'Juli', 'August', 'Septembar', 'Oktobar', 'Novembar', 'Decembar',
  ];

  static const _dayNames = {
    1: 'Ponedjeljak',
    2: 'Utorak',
    3: 'Srijeda',
    4: 'Četvrtak',
    5: 'Petak',
    6: 'Subota',
    7: 'Nedjelja',
  };

  static String monthName(int month) {
    if (month < 1 || month > 12) return '$month';
    return _monthNames[month - 1];
  }

  static String dayName(int dayOfWeek) => _dayNames[dayOfWeek] ?? 'Dan $dayOfWeek';

  /// Minutes -> "12 h 30 min" (course requirement for time-on-platform display).
  static String minutesToHoursAndMinutes(num? totalMinutes) {
    final minutes = (totalMinutes ?? 0).round();
    final hours = minutes ~/ 60;
    final remainder = minutes % 60;
    return '$hours h $remainder min';
  }

  /// Minutes -> "HH:MM:SS" duration display (course rule: no free-text duration entry).
  static String minutesToHms(int totalMinutes) {
    final h = totalMinutes ~/ 60;
    final m = totalMinutes % 60;
    return '${h.toString().padLeft(2, '0')}:${m.toString().padLeft(2, '0')}:00';
  }

  /// Formats like the mobile app (Formatters.price): the backend's only
  /// currency ('usd') prints as "$44.99"; any other code prints as
  /// "44.99 CODE" instead of guessing a fallback currency that was never
  /// actually charged.
  static String money(num? value, {String currency = 'usd'}) {
    final amount = (value ?? 0).toDouble();
    final formatted = amount.toStringAsFixed(2);
    if (currency.toUpperCase() == 'USD') {
      return '\$$formatted';
    }
    return '$formatted $currency';
  }

  static String date(String? isoDate) {
    if (isoDate == null || isoDate.isEmpty) return '-';
    try {
      final parsed = DateTime.parse(isoDate).toLocal();
      return '${parsed.day.toString().padLeft(2, '0')}.${parsed.month.toString().padLeft(2, '0')}.${parsed.year}.';
    } catch (_) {
      return isoDate;
    }
  }

  static String dateTime(String? isoDate) {
    if (isoDate == null || isoDate.isEmpty) return '-';
    try {
      final parsed = DateTime.parse(isoDate).toLocal();
      final d = date(isoDate);
      final hh = parsed.hour.toString().padLeft(2, '0');
      final mm = parsed.minute.toString().padLeft(2, '0');
      return '$d $hh:$mm';
    } catch (_) {
      return isoDate;
    }
  }
}
