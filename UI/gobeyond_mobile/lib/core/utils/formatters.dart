import 'package:intl/intl.dart';

/// Bosnian (ijekavica) date/time/number formatting helpers shared by all
/// screens, so labels read consistently with the mockup captions.
class Formatters {
  const Formatters._();

  static const monthNames = <String>[
    'Januar',
    'Februar',
    'Mart',
    'April',
    'Maj',
    'Juni',
    'Juli',
    'August',
    'Septembar',
    'Oktobar',
    'Novembar',
    'Decembar',
  ];

  static const dayNames = <String>[
    'Ponedjeljak',
    'Utorak',
    'Srijeda',
    'Četvrtak',
    'Petak',
    'Subota',
    'Nedjelja',
  ];

  static const dayShort = <String>[
    'PON',
    'UTO',
    'SRI',
    'ČET',
    'PET',
    'SUB',
    'NED',
  ];

  /// Lowercase accusative forms for "... za PONEDJELJAK/SRIJEDU/SUBOTU..."
  /// (Bosnian "za" governs the accusative case, not the nominative).
  static const dayNamesAccusative = <String>[
    'ponedjeljak',
    'utorak',
    'srijedu',
    'četvrtak',
    'petak',
    'subotu',
    'nedjelju',
  ];

  /// dayOfWeek is 1=Ponedjeljak..7=Nedjelja per the API contract.
  static String dayName(int dayOfWeek) => dayNames[(dayOfWeek - 1).clamp(0, 6)];

  static String dayNameAccusative(int dayOfWeek) =>
      dayNamesAccusative[(dayOfWeek - 1).clamp(0, 6)];

  static String monthName(int month) => monthNames[(month - 1).clamp(0, 11)];

  /// Bosnian plural rule: 1, 21, 31... -> [one]; 2-4, 22-24... -> [few];
  /// everything else (0, 5-20, 25-30...) -> [many]. Also covers "godine"/
  /// "dani" wherever a count needs to agree with a noun.
  static String plural(int n, String one, String few, String many) {
    final mod10 = n % 10;
    final mod100 = n % 100;
    if (mod10 == 1 && mod100 != 11) return one;
    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return few;
    return many;
  }

  static String price(num amount, String? currency) {
    final value = amount.toStringAsFixed(2);
    // The backend returns the currency code lowercased (e.g. "usd").
    if (currency == null ||
        currency.isEmpty ||
        currency.toUpperCase() == 'USD') {
      return '\$$value';
    }
    return '$value $currency';
  }

  /// Renders a duration given in minutes as HH:MM:SS, matching mockup 12
  /// ("TRAJANJE: 01:30:00").
  static String durationFromMinutes(int minutes) {
    final hours = minutes ~/ 60;
    final mins = minutes % 60;
    return '${_pad(hours)}:${_pad(mins)}:00';
  }

  static String _pad(int value) => value.toString().padLeft(2, '0');

  static String dateOnly(DateTime date) =>
      DateFormat('dd.MM.yyyy').format(date);

  static String dateForApi(DateTime date) =>
      DateFormat('yyyy-MM-dd').format(date);

  static DateTime? tryParseIso(String? value) {
    if (value == null || value.isEmpty) return null;
    return DateTime.tryParse(value)?.toLocal();
  }

  static String dateTimeLabel(String? isoValue) {
    final date = tryParseIso(isoValue);
    if (date == null) return '-';
    return DateFormat('dd.MM.yyyy HH:mm').format(date);
  }
}
