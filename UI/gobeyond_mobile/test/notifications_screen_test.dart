import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/data/models/notification_item.dart';
import 'package:gobeyond_mobile/presentation/screens/notifications/notifications_screen.dart';
import 'package:gobeyond_mobile/presentation/widgets/state_views.dart';

import 'support/fakes.dart';

class _CountingNotificationRepository extends FakeNotificationRepository {
  int getNotificationsCalls = 0;
  List<NotificationItem> items = const [];

  /// While set, getNotifications waits for it to complete.
  Completer<void>? gate;

  @override
  Future<List<NotificationItem>> getNotifications(
      {bool unreadOnly = false, String? search}) async {
    getNotificationsCalls++;
    await gate?.future;
    if (search != null && search.trim().isNotEmpty) {
      return items
          .where((i) => i.title.toLowerCase().contains(search.toLowerCase()))
          .toList();
    }
    return items;
  }
}

void main() {
  testWidgets('pull-to-refresh on Obavijesti re-fetches the notifications',
      (tester) async {
    final repository = _CountingNotificationRepository();

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: NotificationsScreen(notificationRepository: repository),
      ),
    );
    await tester.pumpAndSettle();

    final before = repository.getNotificationsCalls;
    await tester.fling(find.byType(ListView).first, const Offset(0, 300), 800);
    await tester.pumpAndSettle();

    expect(repository.getNotificationsCalls, greaterThan(before));
  });

  testWidgets(
      'search with no results shows a distinct message from a truly empty list',
      (tester) async {
    final repository = _CountingNotificationRepository();

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: NotificationsScreen(notificationRepository: repository),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Nemate obavijesti.'), findsOneWidget);

    await tester.enterText(
        find.byType(TextField), 'nešto što sigurno ne postoji');
    await tester.pump(const Duration(milliseconds: 400));
    await tester.pumpAndSettle();

    expect(find.text('Nema obavijesti za zadanu pretragu.'), findsOneWidget);
    expect(find.text('Nemate obavijesti.'), findsNothing);
  });

  testWidgets('pull-to-refresh keeps the notifications on screen while it '
      'reloads', (tester) async {
    final repository = _CountingNotificationRepository()
      ..items = [
        NotificationItem.fromJson({
          'id': 1,
          'title': 'Novi plan',
          'body': 'Mentor je objavio vaš plan.',
          'type': 'PlanPublished',
          'isRead': false,
          'createdAt': DateTime.now().toIso8601String(),
        }),
      ];

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: NotificationsScreen(notificationRepository: repository),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Novi plan'), findsOneWidget);

    final gate = Completer<void>();
    repository.gate = gate;
    await tester.fling(find.text('Novi plan'), const Offset(0, 300), 800);
    await tester.pump();
    await tester.pump(const Duration(seconds: 1));

    expect(repository.getNotificationsCalls, 2);
    expect(find.byType(LoadingView), findsNothing);
    expect(find.text('Novi plan'), findsOneWidget);

    gate.complete();
    await tester.pumpAndSettle();
    expect(find.text('Novi plan'), findsOneWidget);
  });
}
