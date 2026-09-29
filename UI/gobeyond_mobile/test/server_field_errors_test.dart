import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/auth/auth_controller.dart';
import 'package:gobeyond_mobile/core/auth/auth_scope.dart';
import 'package:gobeyond_mobile/core/network/api_exception.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/presentation/screens/auth/registration_form.dart';
import 'package:gobeyond_mobile/presentation/screens/profile/profile_screen.dart';

import 'support/fakes.dart';

// 1080x2400 at 420 dpi in logical px: both forms are longer than the screen.
const _phoneSize = Size(411, 914);

const _dobMessage =
    'Datum rođenja nije ispravan: morate imati između 16 i 100 godina.';

ApiException _validationError(Map<String, List<String>> errors) =>
    ApiException('Podaci nisu ispravni.', errors: errors, statusCode: 400);

Finder _underDobField(String text) => find.ancestor(
      of: find.text(text),
      matching: find.byWidgetPredicate((widget) =>
          widget is InputDecorator &&
          widget.decoration.labelText == 'Datum rođenja'),
    );

double _scrollOffset(WidgetTester tester) =>
    tester.state<ScrollableState>(find.byType(Scrollable).first).position.pixels;

Future<void> _usePhoneSurface(WidgetTester tester) async {
  await tester.binding.setSurfaceSize(_phoneSize);
  addTearDown(() => tester.binding.setSurfaceSize(null));
}

Future<void> _pumpRegistration(
    WidgetTester tester, FakeAuthRepository authRepository) async {
  await _usePhoneSurface(tester);
  final authController = AuthController(
    authRepository: authRepository,
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

/// Fills every required registration field with a locally valid value, so
/// a submit reaches the (fake) server.
Future<void> _fillRegistration(WidgetTester tester) async {
  Future<void> enter(String label, String text) async {
    final field = find.widgetWithText(TextFormField, label);
    await tester.ensureVisible(field);
    await tester.enterText(field, text);
    await tester.pumpAndSettle();
  }

  Future<void> choose(String label, String option) async {
    final dropdown = find.ancestor(
      of: find.text(label),
      matching: find.byWidgetPredicate((w) => w is DropdownButtonFormField),
    );
    await tester.ensureVisible(dropdown);
    await tester.pumpAndSettle();
    await tester.tap(dropdown);
    await tester.pumpAndSettle();
    await tester.tap(find.text(option).last);
    await tester.pumpAndSettle();
  }

  await enter('Ime', 'Test');
  await enter('Prezime', 'Klijent');
  await enter('Korisničko ime', 'test.klijent');
  await enter('Email', 'test@klijent.ba');
  await choose('Spol', 'Muško');
  await enter('Tjelesna težina (kg)', '80');
  await enter('Visina (cm)', '180');
  await choose('Nivo fizičke spreme', 'Početnik');
  await enter('Godine iskustva s treniranjem', '2');
  await choose('Fitness cilj', 'Snaga');
  await enter('Lozinka', 'goodpass1');
  await enter('Potvrdi lozinku', 'goodpass1');

  await tester.ensureVisible(find.text('Datum rođenja'));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Datum rođenja'), warnIfMissed: false);
  await tester.pumpAndSettle();
  // No Bosnian localization delegates in this MaterialApp, so the picker's
  // confirm button is the English "OK"; it keeps the valid initial date.
  await tester.tap(find.text('OK'));
  await tester.pumpAndSettle();
}

Future<void> _submitRegistration(WidgetTester tester) async {
  await tester.ensureVisible(find.text('REGISTRUJ SE'));
  await tester.pumpAndSettle();
  expect(_scrollOffset(tester), greaterThan(0));
  await tester.tap(find.text('REGISTRUJ SE'));
  await tester.pumpAndSettle();
}

Future<void> _pumpProfile(
    WidgetTester tester, FakeProfileRepository profileRepository) async {
  await _usePhoneSurface(tester);
  final authController = AuthController(
    authRepository: FakeAuthRepository(),
    profileRepository: profileRepository,
    activityRepository: FakeActivityRepository(),
  );
  await authController.refreshProfile();
  await tester.pumpWidget(
    AuthScope(
      controller: authController,
      child: MaterialApp(
        theme: AppTheme.theme,
        home: ProfileScreen(
          lookupRepository: FakeLookupRepository(),
          profileRepository: profileRepository,
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> _submitProfile(WidgetTester tester) async {
  await tester.ensureVisible(find.text('SPREMI IZMJENE'));
  await tester.pumpAndSettle();
  expect(_scrollOffset(tester), greaterThan(0));
  await tester.tap(find.text('SPREMI IZMJENE'));
  await tester.pumpAndSettle();
}

void main() {
  group('registration', () {
    testWidgets('a server dateOfBirth error shows under the date field',
        (tester) async {
      final authRepository = FakeAuthRepository()
        ..registerError = _validationError({
          'dateOfBirth': [_dobMessage],
        });
      await _pumpRegistration(tester, authRepository);
      await _fillRegistration(tester);

      await _submitRegistration(tester);

      expect(_underDobField(_dobMessage), findsOneWidget);
    });

    testWidgets('a failed submit scrolls back up to the field errors',
        (tester) async {
      const usernameTaken = 'Korisničko ime je već zauzeto.';
      final authRepository = FakeAuthRepository()
        ..registerError = _validationError({
          'username': [usernameTaken],
        });
      await _pumpRegistration(tester, authRepository);
      await _fillRegistration(tester);

      await _submitRegistration(tester);

      expect(_scrollOffset(tester), 0);
      final error = tester.getRect(find.text(usernameTaken));
      expect(error.top, greaterThanOrEqualTo(0));
      expect(error.bottom, lessThanOrEqualTo(_phoneSize.height));
    });
  });

  group('profile', () {
    testWidgets('a server dateOfBirth error shows under the date field',
        (tester) async {
      final profileRepository = FakeProfileRepository()
        ..updateError = _validationError({
          'dateOfBirth': [_dobMessage],
        });
      await _pumpProfile(tester, profileRepository);

      await _submitProfile(tester);

      expect(_underDobField(_dobMessage), findsOneWidget);
    });

    testWidgets('a failed save scrolls back up to the field errors',
        (tester) async {
      final profileRepository = FakeProfileRepository()
        ..updateError = _validationError({
          'dateOfBirth': [_dobMessage],
        });
      await _pumpProfile(tester, profileRepository);

      await _submitProfile(tester);

      expect(_scrollOffset(tester), 0);
    });
  });
}
