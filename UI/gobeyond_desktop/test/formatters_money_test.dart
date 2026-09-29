import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_desktop/core/theme/app_theme.dart';
import 'package:gobeyond_desktop/core/utils/formatters.dart';
import 'package:gobeyond_desktop/presentation/screens/mentor_register_screen.dart';

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

  // The monthly price input still said "(1-1000 KM)" next to a validator
  // that had just been taught to enforce a 2-decimal USD price, contradicting
  // the "$" shown everywhere else. Checked on the real mentor registration
  // screen (the simplest of the three price inputs to reach without backend
  // data — profile_screen.dart and admin/widgets/user_edit_dialog.dart apply
  // the identical one-line label fix).
  testWidgets('the mentor registration price field is labelled in dollars, not KM', (tester) async {
    await tester.pumpWidget(MaterialApp(theme: AppTheme.dark, home: const Scaffold(body: MentorRegisterScreen())));
    // Let the (failing, no backend in the test environment) reference-data
    // fetch settle so the form renders, like reference_data_form_test.dart.
    await tester.pump(const Duration(seconds: 1));

    expect(find.textContaining('KM)'), findsNothing);
    expect(find.widgetWithText(TextFormField, 'Mjesečna cijena (1-1000 \$)'), findsOneWidget);
  });
}
