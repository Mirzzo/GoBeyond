import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_desktop/core/theme/app_theme.dart';
import 'package:gobeyond_desktop/presentation/screens/admin/admin_clients_screen.dart';
import 'package:gobeyond_desktop/presentation/screens/admin/admin_subscriptions_screen.dart';
import 'package:gobeyond_desktop/presentation/widgets/dialogs.dart';
import 'package:gobeyond_desktop/presentation/widgets/panel.dart';

import 'support/fake_api_interceptor.dart';

/// A charge disputed at the client's bank is not refunded (payment status
/// `Disputed`, api-contract.md §1 and §5). The admin sees it as "Osporeno" in
/// the subscription details, and an admin cancel or user deletion that left
/// such a charge unrefunded shows the response `warning` in a dialog.
void main() {
  const warning = 'Uplata od 29,99 USD je osporena kod banke klijenta i nije vraćena; '
      'ishod rješava postupak osporavanja na Stripe-u.';

  group('PaymentStatusPresentation', () {
    test('labels every payment status in Bosnian, Disputed as Osporeno', () {
      expect(PaymentStatusPresentation.label('Disputed'), 'Osporeno');
      expect(PaymentStatusPresentation.label('Succeeded'), 'Uspješno');
      expect(PaymentStatusPresentation.label('Refunded'), 'Refundirano');
      expect(PaymentStatusPresentation.label('RefundPending'), 'Povrat novca u obradi');
      expect(PaymentStatusPresentation.label('Pending'), 'Na čekanju');
      expect(PaymentStatusPresentation.label('Failed'), 'Neuspješno');
    });

    test('shows an unknown status as it is', () {
      expect(PaymentStatusPresentation.label('SomethingNew'), 'SomethingNew');
    });
  });

  Map<String, dynamic> subscription({required String status, required List<Map<String, dynamic>> payments}) => {
        'id': 7,
        'clientFullName': 'Amina Hodžić',
        'mentorFullName': 'Kenan Omerović',
        'trainingTypeName': 'Weightlifting',
        'status': status,
        'price': 29.99,
        'currency': 'usd',
        'createdAt': '2026-09-29T10:00:00Z',
        'startDate': null,
        'endDate': null,
        'statusReason': null,
        'payments': payments,
      };

  Map<String, dynamic> payment(String status, num amount) => {
        'amount': amount,
        'currency': 'usd',
        'purpose': 'Initial',
        'status': status,
        'createdAt': '2026-09-29T10:00:00Z',
        'paidAt': '2026-09-29T10:01:00Z',
      };

  FakeApiInterceptor installApi(WidgetTester tester) {
    tester.view.physicalSize = const Size(1600, 1000);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);
    final api = FakeApiInterceptor.install();
    addTearDown(api.uninstall);
    return api;
  }

  Future<void> show(WidgetTester tester, Widget screen) async {
    await tester.pumpWidget(MaterialApp(
      theme: AppTheme.dark,
      // The test font draws every glyph as a full square, much wider than the
      // app's font; scaled down, fixed-width controls do not overflow.
      builder: (context, child) =>
          MediaQuery(data: MediaQuery.of(context).copyWith(textScaler: const TextScaler.linear(0.5)), child: child!),
      home: Scaffold(body: screen),
    ));
    await tester.pumpAndSettle();
  }

  testWidgets('subscription details list the payments and show a disputed one as Osporeno', (tester) async {
    final api = installApi(tester);
    api.on('GET', '/api/admin/subscriptions', (options, handler) {
      handler.resolve(fakeResponse(options, [
        subscription(status: 'Cancelled', payments: [payment('Refunded', 12.5), payment('Disputed', 29.99)]),
      ]));
    });
    await show(tester, const AdminSubscriptionsScreen());

    expect(find.byTooltip('Uplata je osporena kod banke klijenta i nije vraćena.'), findsOneWidget);
    await tester.tap(find.text('DETALJI'));
    await tester.pumpAndSettle();

    expect(find.text('Uplate'), findsOneWidget);
    expect(find.widgetWithText(StatusChip, 'Osporeno'), findsOneWidget);
    expect(find.widgetWithText(StatusChip, 'Refundirano'), findsOneWidget);
    expect(find.textContaining('Početna uplata · \$29.99'), findsOneWidget);
  });

  testWidgets('a subscription without payments says so and has no dispute marker', (tester) async {
    final api = installApi(tester);
    api.on('GET', '/api/admin/subscriptions', (options, handler) {
      handler.resolve(fakeResponse(options, [subscription(status: 'PendingPayment', payments: [])]));
    });
    await show(tester, const AdminSubscriptionsScreen());

    expect(find.byTooltip('Uplata je osporena kod banke klijenta i nije vraćena.'), findsNothing);
    await tester.tap(find.text('DETALJI'));
    await tester.pumpAndSettle();

    expect(find.text('Nema uplata.'), findsOneWidget);
  });

  Future<void> cancel(WidgetTester tester, {required String? responseWarning}) async {
    final api = installApi(tester);
    api.on('GET', '/api/admin/subscriptions', (options, handler) {
      handler.resolve(fakeResponse(options, [
        subscription(status: 'AwaitingMentor', payments: [payment('Succeeded', 29.99)]),
      ]));
    });
    api.on('PUT', '/api/admin/subscriptions/7/cancel', (options, handler) {
      handler.resolve(fakeResponse(options, {
        ...subscription(status: 'Cancelled', payments: [payment(responseWarning == null ? 'Refunded' : 'Disputed', 29.99)]),
        'warning': responseWarning,
      }));
    });
    await show(tester, const AdminSubscriptionsScreen());

    await tester.tap(find.text('DETALJI'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ElevatedButton, 'OTKAŽI'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextFormField), 'Mentor ne odgovara na zahtjev');
    await tester.tap(find.widgetWithText(ElevatedButton, 'OTKAŽI'));
    await tester.pumpAndSettle();
  }

  testWidgets('admin cancel that left a disputed charge unrefunded shows the warning', (tester) async {
    await cancel(tester, responseWarning: warning);

    expect(find.text('Pretplata je otkazana.'), findsOneWidget);
    expect(find.text('Uplata nije vraćena'), findsOneWidget);
    expect(find.text(warning), findsOneWidget);

    await tester.tap(find.widgetWithText(ElevatedButton, 'U redu'));
    await tester.pumpAndSettle();
    expect(find.text('Uplata nije vraćena'), findsNothing);
  });

  testWidgets('admin cancel without a disputed charge shows no warning', (tester) async {
    await cancel(tester, responseWarning: null);

    expect(find.text('Pretplata je otkazana.'), findsOneWidget);
    expect(find.text('Uplata nije vraćena'), findsNothing);
  });

  testWidgets('deleting a client whose charge is disputed shows the warning', (tester) async {
    final api = installApi(tester);
    api.on('GET', '/api/fitness-goals', (options, handler) {
      handler.resolve(fakeResponse(options, {'items': <Object>[], 'totalCount': 0}));
    });
    api.on('GET', '/api/admin/clients', (options, handler) {
      handler.resolve(fakeResponse(options, [
        {
          'userId': 21,
          'clientProfileId': 5,
          'fullName': 'Amina Hodžić',
          'username': 'amina',
          'email': 'amina@example.org',
          'profileImageUrl': null,
          'fitnessGoalName': 'Snaga',
          'fitnessLevelName': 'Početnik',
          'activeMentorName': null,
          'isActive': true,
        },
      ]));
    });
    api.on('DELETE', '/api/admin/users/21', (options, handler) {
      handler.resolve(fakeResponse(options, {'message': 'Korisnik Amina Hodžić je obrisan.', 'warning': warning}));
    });
    await show(tester, const AdminClientsScreen());

    await tester.tap(find.widgetWithText(PillButton, 'OBRIŠI'));
    await tester.pumpAndSettle();
    await tester.tap(find.descendant(of: find.byType(GbDialog), matching: find.widgetWithText(ElevatedButton, 'OBRIŠI')));
    await tester.pumpAndSettle();

    expect(find.text('Klijent Amina Hodžić je obrisan.'), findsOneWidget);
    expect(find.text('Uplata nije vraćena'), findsOneWidget);
    expect(find.text(warning), findsOneWidget);
  });
}
