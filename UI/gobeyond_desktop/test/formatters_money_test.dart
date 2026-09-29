import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_desktop/core/utils/formatters.dart';

/// dsk-currency-raw-lowercase: the desktop showed raw lowercase codes like
/// "44.99 usd" (the API's only currency) and fell back to a currency
/// ('BAM') the backend never actually charges in, unlike the mobile app's
/// Formatters.price ("$44.99"). This mirrors that behaviour.
void main() {
  group('Formatters.money', () {
    test('formats the backend currency (usd) as a dollar amount, like mobile', () {
      expect(Formatters.money(44.99, currency: 'usd'), r'$44.99');
    });

    test('is case-insensitive for USD', () {
      expect(Formatters.money(44.99, currency: 'USD'), r'$44.99');
      expect(Formatters.money(44.99, currency: 'Usd'), r'$44.99');
    });

    test('defaults to usd (never the hardcoded BAM fallback) when no currency is given', () {
      expect(Formatters.money(5), r'$5.00');
    });

    test('formats any other currency code as "amount CODE"', () {
      expect(Formatters.money(179.96, currency: 'eur'), '179.96 eur');
    });

    test('treats a null amount as zero', () {
      expect(Formatters.money(null), r'$0.00');
    });

    test('always shows two decimals', () {
      expect(Formatters.money(923.7, currency: 'usd'), r'$923.70');
    });
  });
}
