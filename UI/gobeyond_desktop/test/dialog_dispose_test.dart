import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:gobeyond_desktop/core/services/admin_service.dart';
import 'package:gobeyond_desktop/core/theme/app_theme.dart';
import 'package:gobeyond_desktop/presentation/screens/admin/admin_announcements_screen.dart';
import 'package:gobeyond_desktop/presentation/screens/admin/admin_reference_data_screen.dart';
import 'package:gobeyond_desktop/presentation/screens/admin/widgets/reset_password_dialog.dart';
import 'package:gobeyond_desktop/presentation/screens/profile_screen.dart';
import 'package:gobeyond_desktop/presentation/widgets/dialogs.dart';

import 'support/fake_api_interceptor.dart';

/// dsk-dialog-controller-disposed-crash: every one of these dialogs used to
/// create its TextEditingController(s) in a plain function and dispose them
/// by hand right after `await showDialog(...)`/`showGbDialog(...)` returned.
/// Navigator.pop completes that future immediately, but the dialog route's
/// exit transition kept the (still-focused) TextFormField mounted a little
/// longer, so it rebuilt and tried to add a listener to the
/// already-disposed controller — throwing "A TextEditingController was used
/// after being disposed." on every real reject/cancel/reset/announcement/
/// šifarnik/password-change, followed by an Overlay assertion that broke
/// the whole screen. Each dialog body is now a StatefulWidget that owns its
/// controllers via initState/dispose, so typing into the field (to give it
/// focus, exactly like the real repro) and then closing it — by submit,
/// Odustani or the X — must not throw.
void main() {
  Widget wrap(Widget child) => MaterialApp(theme: AppTheme.dark, home: Scaffold(body: child));

  group('showReasonDialog (dialogs.dart)', () {
    Widget buildApp() => wrap(Builder(
          builder: (context) => ElevatedButton(
            onPressed: () => showReasonDialog(context, title: 'Odbij zahtjev', label: 'Razlog', minLength: 10, maxLength: 500),
            child: const Text('open'),
          ),
        ));

    testWidgets('typing a reason then confirming does not throw', (tester) async {
      await tester.pumpWidget(buildApp());
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      final field = find.byType(TextFormField);
      await tester.tap(field);
      await tester.enterText(field, 'Ovo je dovoljno dug razlog za test.');
      await tester.tap(find.widgetWithText(ElevatedButton, 'Potvrdi'));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });

    testWidgets('typing a reason then pressing Odustani does not throw', (tester) async {
      await tester.pumpWidget(buildApp());
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      final field = find.byType(TextFormField);
      await tester.tap(field);
      await tester.enterText(field, 'Ovo je dovoljno dug razlog za test.');
      await tester.tap(find.widgetWithText(TextButton, 'Odustani'));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });
  });

  group('showResetPasswordDialog', () {
    Widget buildApp() => wrap(Builder(
          builder: (context) => ElevatedButton(
            onPressed: () => showResetPasswordDialog(context, userId: 1, fullName: 'Test Korisnik'),
            child: const Text('open'),
          ),
        ));

    testWidgets('typing a new password then pressing Odustani does not throw', (tester) async {
      await tester.pumpWidget(buildApp());
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      final field = find.widgetWithText(TextFormField, 'Nova lozinka');
      await tester.tap(field);
      await tester.enterText(field, 'novalozinka1');
      await tester.tap(find.widgetWithText(TextButton, 'Odustani'));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });

    testWidgets('typing a new password then closing with X does not throw', (tester) async {
      await tester.pumpWidget(buildApp());
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      final field = find.widgetWithText(TextFormField, 'Nova lozinka');
      await tester.tap(field);
      await tester.enterText(field, 'novalozinka1');
      await tester.tap(find.byIcon(Icons.close));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });
  });

  group('closing a dialog while a failing request is in flight', () {
    // Every _submit() catch block used to call setState()/_formKey.currentState!
    // before checking `mounted`. Closing the dialog while the request it fired
    // is still pending, then letting that request fail, ran that code after
    // dispose() — "setState() called after dispose()". A real fake-backend
    // round trip (not just typing+closing with no request in flight) is
    // needed to reproduce it.
    testWidgets('reset-password dialog closed with X before the request fails does not throw', (tester) async {
      final api = FakeApiInterceptor.install();
      addTearDown(api.uninstall);
      RequestInterceptorHandler? pendingHandler;
      RequestOptions? pendingOptions;
      api.on('PUT', '/api/admin/users/1/reset-password', (options, handler) {
        pendingOptions = options;
        pendingHandler = handler;
      });

      await tester.pumpWidget(wrap(Builder(
        builder: (context) => ElevatedButton(
          onPressed: () => showResetPasswordDialog(context, userId: 1, fullName: 'Test Korisnik'),
          child: const Text('open'),
        ),
      )));
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.enterText(find.widgetWithText(TextFormField, 'Nova lozinka'), 'novalozinka1');
      await tester.enterText(find.widgetWithText(TextFormField, 'Potvrdite novu lozinku'), 'novalozinka1');
      await tester.tap(find.widgetWithText(ElevatedButton, 'RESETUJ LOZINKU'));
      await tester.pump(Duration.zero);

      expect(pendingHandler, isNotNull, reason: 'the request should have reached the fake backend');

      await tester.tap(find.byIcon(Icons.close));
      await tester.pumpAndSettle();

      pendingHandler!.reject(fakeError(pendingOptions!, statusCode: 400, data: {'message': 'Resetovanje nije uspjelo.'}));
      await tester.pump(Duration.zero);

      expect(tester.takeException(), isNull);
    });
  });

  group('Sistemske obavijesti — NOVA OBAVIJEST form', () {
    // Driven directly through showAnnouncementDialog (not the full
    // AdminAnnouncementsScreen): the screen's own initState also fires an
    // unrelated, pre-existing unguarded ReferenceDataService.roles() call
    // that rejects with no backend in the test environment, which is
    // unrelated to this bug and would make this test flaky.
    Widget buildApp() => wrap(Builder(
          builder: (context) => ElevatedButton(
            onPressed: () => showAnnouncementDialog(context, service: AdminService(), roles: const []),
            child: const Text('open'),
          ),
        ));

    testWidgets('typing a title then pressing Odustani does not throw', (tester) async {
      await tester.pumpWidget(buildApp());
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      final field = find.widgetWithText(TextFormField, 'Naslov');
      await tester.tap(field);
      await tester.enterText(field, 'Test naslov');
      await tester.tap(find.widgetWithText(TextButton, 'Odustani'));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });
  });

  group('Šifarnici — DODAJ form', () {
    testWidgets('typing a name then pressing Odustani does not throw', (tester) async {
      await tester.pumpWidget(wrap(const AdminReferenceDataScreen()));
      await tester.pump(const Duration(seconds: 1));

      await tester.tap(find.widgetWithText(ElevatedButton, 'DODAJ'));
      await tester.pumpAndSettle();

      final field = find.widgetWithText(TextFormField, 'Naziv');
      await tester.tap(field);
      await tester.enterText(field, 'Test naziv');
      await tester.tap(find.widgetWithText(TextButton, 'Odustani'));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });
  });

  group('Profil — Promjena lozinke form', () {
    testWidgets('typing a password then pressing Odustani does not throw', (tester) async {
      await tester.pumpWidget(wrap(const ProfileScreen()));
      await tester.pump(const Duration(seconds: 1));

      await tester.tap(find.widgetWithText(OutlinedButton, 'Promijeni lozinku'));
      await tester.pumpAndSettle();

      final field = find.widgetWithText(TextFormField, 'Trenutna lozinka');
      await tester.tap(field);
      await tester.enterText(field, 'trenutna1');
      await tester.tap(find.widgetWithText(TextButton, 'Odustani'));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });

    testWidgets('typing a password then closing with X does not throw', (tester) async {
      await tester.pumpWidget(wrap(const ProfileScreen()));
      await tester.pump(const Duration(seconds: 1));

      await tester.tap(find.widgetWithText(OutlinedButton, 'Promijeni lozinku'));
      await tester.pumpAndSettle();

      final field = find.widgetWithText(TextFormField, 'Trenutna lozinka');
      await tester.tap(field);
      await tester.enterText(field, 'trenutna1');
      await tester.tap(find.byIcon(Icons.close));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });
  });
}
