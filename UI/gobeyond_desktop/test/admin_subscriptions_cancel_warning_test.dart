import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_desktop/core/theme/app_theme.dart';
import 'package:gobeyond_desktop/presentation/screens/admin/admin_subscriptions_screen.dart';

import 'support/fake_api_interceptor.dart';

/// The OTKAŽI confirmation dialog must describe what admin cancel really
/// does (api-contract.md §5): the client is always notified, the mentor only
/// when the request reached them (never for an unpaid PendingPayment
/// subscription), and a paid AwaitingMentor request is refunded.
void main() {
  group('subscriptionCancelWarning', () {
    test('mentions the refund for an AwaitingMentor subscription', () {
      final warning = subscriptionCancelWarning(
        status: 'AwaitingMentor',
        clientFullName: 'Amina Hodžić',
        mentorFullName: 'Kenan Omerović',
      );
      expect(warning, contains('Amina Hodžić'));
      expect(warning, contains('Kenan Omerović'));
      expect(warning, contains('uplata će biti vraćena'));
    });

    test('names both parties and no refund for an Active subscription', () {
      final warning = subscriptionCancelWarning(
        status: 'Active',
        clientFullName: 'Amina Hodžić',
        mentorFullName: 'Kenan Omerović',
      );
      expect(warning, 'Klijent (Amina Hodžić) i mentor (Kenan Omerović) će biti obaviješteni o otkazivanju.');
    });

    test('says only the client is notified for a PendingPayment subscription', () {
      final warning = subscriptionCancelWarning(
        status: 'PendingPayment',
        clientFullName: 'Amina Hodžić',
        mentorFullName: 'Kenan Omerović',
      );
      expect(warning, 'Klijent (Amina Hodžić) dobija obavijest o otkazivanju. Mentor ne dobija obavijest jer zahtjev nije plaćen.');
      expect(warning, isNot(contains('Kenan Omerović')));
      expect(warning, isNot(contains('vraćena')));
    });
  });

  group('OTKAŽI dialog on the real screen', () {
    Future<String> openCancelWarning(WidgetTester tester, String status) async {
      tester.view.physicalSize = const Size(1600, 1000);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.reset);
      final api = FakeApiInterceptor.install();
      addTearDown(api.uninstall);
      api.on('GET', '/api/admin/subscriptions', (options, handler) {
        handler.resolve(fakeResponse(options, [
          {
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
          },
        ]));
      });

      await tester.pumpWidget(MaterialApp(
        theme: AppTheme.dark,
        // The test font draws every glyph as a full square, much wider than the
        // app's font; scaled down, the fixed-width status filter does not overflow.
        builder: (context, child) =>
            MediaQuery(data: MediaQuery.of(context).copyWith(textScaler: const TextScaler.linear(0.5)), child: child!),
        home: const Scaffold(body: AdminSubscriptionsScreen()),
      ));
      await tester.pumpAndSettle();
      await tester.tap(find.text('DETALJI'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ElevatedButton, 'OTKAŽI'));
      await tester.pumpAndSettle();

      final warningRow = find.ancestor(of: find.byIcon(Icons.warning_amber_rounded), matching: find.byType(Row)).first;
      return tester.widget<Text>(find.descendant(of: warningRow, matching: find.byType(Text))).data!;
    }

    testWidgets('PendingPayment: the dialog names only the client', (tester) async {
      final warning = await openCancelWarning(tester, 'PendingPayment');

      expect(warning, 'Klijent (Amina Hodžić) dobija obavijest o otkazivanju. Mentor ne dobija obavijest jer zahtjev nije plaćen.');
    });

    testWidgets('AwaitingMentor: the dialog names both parties and the refund', (tester) async {
      final warning = await openCancelWarning(tester, 'AwaitingMentor');

      expect(warning, 'Klijent (Amina Hodžić) i mentor (Kenan Omerović) će biti obaviješteni o otkazivanju. '
          'Klijentova uplata će biti vraćena.');
    });
  });
}
