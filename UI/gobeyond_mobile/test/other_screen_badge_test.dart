import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/data/models/notification_item.dart';
import 'package:gobeyond_mobile/presentation/screens/other/other_screen.dart';

import 'support/fakes.dart';

class _UnreadNotificationRepository extends FakeNotificationRepository {
  int unreadCount;
  _UnreadNotificationRepository(this.unreadCount);

  @override
  Future<int> getUnreadCount() async => unreadCount;

  @override
  Future<List<NotificationItem>> getNotifications(
          {bool unreadOnly = false, String? search}) async =>
      const [];
}

void main() {
  testWidgets('Obavijesti tile in Ostalo shows the unread count badge',
      (tester) async {
    final repository = _UnreadNotificationRepository(6);

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: OtherScreen(notificationRepository: repository),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('6'), findsOneWidget);
  });

  testWidgets('no badge is shown when there are no unread notifications',
      (tester) async {
    final repository = _UnreadNotificationRepository(0);

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: OtherScreen(notificationRepository: repository),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('0'), findsNothing);
  });
}
