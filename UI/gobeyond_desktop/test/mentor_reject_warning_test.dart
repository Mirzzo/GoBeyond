import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_desktop/core/theme/app_theme.dart';
import 'package:gobeyond_desktop/presentation/screens/mentor/mentor_collaboration_requests_screen.dart';

import 'support/fake_api_interceptor.dart';

/// The ODBIJ dialog must not promise an unconditional refund: a charge that
/// is disputed at the client's bank is not refunded (api-contract.md §6).
void main() {
  const expected = 'Klijentu Amina Hodžić će biti vraćen novac za uplatu (refundacija), '
      'osim ako je naplata osporena kod banke klijenta.';

  test('collaborationRejectWarning names the disputed-charge exception', () {
    expect(collaborationRejectWarning('Amina Hodžić'), expected);
  });

  testWidgets('ODBIJ dialog on the real screen shows the warning', (tester) async {
    tester.view.physicalSize = const Size(1600, 1000);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);
    final api = FakeApiInterceptor.install();
    addTearDown(api.uninstall);
    api.on('GET', '/api/mentors/me/collaboration-requests', (options, handler) {
      handler.resolve(fakeResponse(options, [
        {
          'subscriptionId': 7,
          'clientFullName': 'Amina Hodžić',
          'clientPhotoUrl': null,
          'status': 'AwaitingMentor',
          'requestedAt': '2026-09-29T10:00:00Z',
          'planId': null,
          'planStatus': null,
        },
      ]));
    });
    api.on('GET', '/api/mentors/me/collaboration-requests/7', (options, handler) {
      handler.resolve(fakeResponse(options, {
        'subscriptionId': 7,
        'status': 'AwaitingMentor',
        'clientFullName': 'Amina Hodžić',
        'clientPhotoUrl': null,
        'questionnaire': <String, dynamic>{},
      }));
    });

    await tester.pumpWidget(MaterialApp(
      theme: AppTheme.dark,
      // The test font draws every glyph as a full square, much wider than the
      // app's font; scaled down, fixed-width controls do not overflow.
      builder: (context, child) =>
          MediaQuery(data: MediaQuery.of(context).copyWith(textScaler: const TextScaler.linear(0.5)), child: child!),
      home: const Scaffold(body: MentorCollaborationRequestsScreen()),
    ));
    await tester.pumpAndSettle();
    await tester.tap(find.text('PREGLED..'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(OutlinedButton, 'ODBIJ'));
    await tester.pumpAndSettle();

    final warningRow = find.ancestor(of: find.byIcon(Icons.warning_amber_rounded), matching: find.byType(Row)).first;
    expect(tester.widget<Text>(find.descendant(of: warningRow, matching: find.byType(Text))).data, expected);
  });
}
