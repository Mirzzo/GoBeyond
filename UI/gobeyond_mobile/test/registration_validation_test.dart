import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/auth/auth_controller.dart';
import 'package:gobeyond_mobile/core/auth/auth_scope.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/core/utils/validators.dart';
import 'package:gobeyond_mobile/presentation/screens/auth/registration_form.dart';

import 'support/fakes.dart';

void main() {
  group('Validators (registration rules)', () {
    test('email requires ime@domena.com format', () {
      expect(Validators.email(''), 'Email je obavezan.');
      expect(Validators.email('bademail'),
          'Unesite email u formatu ime@domena.com.');
      expect(Validators.email('dobar@email.com'), isNull);
    });

    test('username enforces 3-30 chars and allowed characters', () {
      expect(
        Validators.username('ab'),
        'Korisničko ime mora imati 3–30 znakova (slova, brojevi, tačka, donja crta).',
      );
      expect(Validators.username('dobar.korisnik1'), isNull);
    });

    test('password requires 8-64 chars with a letter and a number', () {
      expect(
        Validators.password('short'),
        'Lozinka mora imati 8–64 znaka, uključujući barem jedno slovo i jedan broj.',
      );
      expect(Validators.password('alllettersnodigits'), isNotNull);
      expect(Validators.password('goodpass1'), isNull);
    });

    test('confirmPassword must match the original', () {
      expect(Validators.confirmPassword('other', 'goodpass1'),
          'Lozinke se ne podudaraju.');
      expect(Validators.confirmPassword('goodpass1', 'goodpass1'), isNull);
    });

    test('numberRange enforces weight bounds with an explicit message', () {
      expect(
        Validators.numberRange('10', min: 30, max: 300, label: 'Težina'),
        'Težina mora biti između 30 i 300.',
      );
      expect(Validators.numberRange('80', min: 30, max: 300, label: 'Težina'),
          isNull);
    });
  });

  group('RegistrationForm widget', () {
    testWidgets(
        'shows explicit validation messages for empty/invalid fields on submit',
        (tester) async {
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

      await tester.enterText(
          find.widgetWithText(TextFormField, 'Email'), 'bademail');
      await tester.enterText(
          find.widgetWithText(TextFormField, 'Korisničko ime'), 'ab');

      await tester.ensureVisible(find.text('REGISTRUJ SE'));
      await tester.tap(find.text('REGISTRUJ SE'));
      await tester.pumpAndSettle();

      expect(
          find.text('Unesite email u formatu ime@domena.com.'), findsOneWidget);
      expect(
        find.text(
            'Korisničko ime mora imati 3–30 znakova (slova, brojevi, tačka, donja crta).'),
        findsOneWidget,
      );
      expect(find.text('Ime je obavezno (2–50 znakova).'), findsOneWidget);
    });
  });
}
