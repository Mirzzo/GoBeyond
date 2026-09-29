import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/auth/auth_controller.dart';
import 'package:gobeyond_mobile/core/auth/auth_scope.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/presentation/screens/auth/registration_form.dart';

import 'support/fakes.dart';

void main() {
  Future<void> pumpForm(WidgetTester tester) async {
    final authController = AuthController(
      authRepository: FakeAuthRepository(),
      profileRepository: FakeProfileRepository(),
      activityRepository: FakeActivityRepository(),
    );

    await tester.pumpWidget(
      AuthScope(
        controller: authController,
        child: MaterialApp(
          theme: AppTheme.theme,
          home: Scaffold(
            body: RegistrationForm(lookupRepository: FakeLookupRepository()),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets(
      'submitting without a DOB shows an inline error under the field, not just a bottom message',
      (tester) async {
    await pumpForm(tester);

    await tester.ensureVisible(find.text('REGISTRUJ SE'));
    await tester.tap(find.text('REGISTRUJ SE'));
    await tester.pumpAndSettle();

    final errorText = find.text('Datum rođenja je obavezan.');
    expect(errorText, findsOneWidget);

    // Not just present anywhere on the form (e.g. a bottom banner) — it
    // must render inside the DOB field's own InputDecorator, the same way
    // Flutter renders any other TextFormField's validation error.
    final dobDecorator = find.ancestor(
      of: errorText,
      matching: find.byWidgetPredicate((widget) =>
          widget is InputDecorator &&
          widget.decoration.labelText == 'Datum rođenja'),
    );
    expect(dobDecorator, findsOneWidget);
  });

  testWidgets('picking a date clears the DOB error', (tester) async {
    await pumpForm(tester);

    await tester.ensureVisible(find.text('REGISTRUJ SE'));
    await tester.tap(find.text('REGISTRUJ SE'));
    await tester.pumpAndSettle();
    expect(find.text('Datum rođenja je obavezan.'), findsOneWidget);

    await tester.ensureVisible(find.text('Datum rođenja'));
    await tester.tap(find.text('Datum rođenja'), warnIfMissed: false);
    await tester.pumpAndSettle();
    // This test's MaterialApp has no Bosnian localization delegates (unlike
    // the real app's main.dart), so the Material date picker's confirm
    // button reads the English default "OK"; it confirms the picker's
    // initial date (a valid 20-year-old DOB per the picker's range).
    await tester.tap(find.text('OK'));
    await tester.pumpAndSettle();

    expect(find.text('Datum rođenja je obavezan.'), findsNothing);
  });
}
