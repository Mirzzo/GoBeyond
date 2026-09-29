import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/data/models/subscription.dart';
import 'package:gobeyond_mobile/presentation/screens/subscription/subscription_detail_screen.dart';

import 'support/fakes.dart';

Map<String, dynamic> _payment(String status, {String purpose = 'Initial'}) => {
      'amount': 19.99,
      'currency': 'USD',
      'purpose': purpose,
      'status': status,
      'createdAt': '2026-09-01T10:00:00Z',
      'paidAt': '2026-09-01T10:01:00Z',
    };

Future<void> _pumpDetail(
    WidgetTester tester, List<Map<String, dynamic>> payments) async {
  final repository = FakeSubscriptionRepository()
    ..subscriptions = [
      Subscription.fromJson({
        'id': 7,
        'mentorProfileId': 1,
        'mentorFullName': 'Marko Marković',
        'trainingTypeName': 'Weightlifting',
        'status': 'Cancelled',
        'price': 19.99,
        'currency': 'USD',
        'createdAt': '2026-09-01T10:00:00Z',
        'canReview': false,
        'canRenew': false,
        'canCancel': false,
        'payments': payments,
      }),
    ];
  await tester.pumpWidget(
    MaterialApp(
      theme: AppTheme.theme,
      home: SubscriptionDetailScreen(
        subscriptionId: 7,
        subscriptionRepository: repository,
      ),
    ),
  );
  await tester.pumpAndSettle();
}

/// The status icon on the payment row whose status line contains [label].
Icon _iconOfPayment(WidgetTester tester, String label) {
  final row = find
      .ancestor(of: find.textContaining(label), matching: find.byType(Row))
      .first;
  return tester.widget<Icon>(
      find.descendant(of: row, matching: find.byType(Icon)).first);
}

void main() {
  testWidgets('a disputed payment reads "Osporeno" with its own icon, not the '
      'raw status with the pending hourglass', (tester) async {
    await _pumpDetail(tester, [
      _payment('Disputed'),
      _payment('Refunded', purpose: 'Renewal'),
    ]);

    expect(find.textContaining('Osporeno · '), findsOneWidget);
    expect(find.textContaining('Disputed'), findsNothing);

    final disputed = _iconOfPayment(tester, 'Osporeno · ');
    expect(disputed.icon, Icons.gavel_rounded);
    expect(disputed.color, AppTheme.accent);
    expect(find.byIcon(Icons.hourglass_top_rounded), findsNothing);

    // The other statuses keep their own labels and icons.
    expect(_iconOfPayment(tester, 'Refundirano · ').icon, Icons.undo_rounded);
  });
}
