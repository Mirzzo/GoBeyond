import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:gobeyond_desktop/core/session/session_controller.dart';
import 'package:gobeyond_desktop/core/theme/app_theme.dart';
import 'package:gobeyond_desktop/presentation/screens/notifications_screen.dart';

import 'support/fake_api_interceptor.dart';

/// dsk-notifications-screen-no-refresh: the Obavijesti screen only ever
/// called `_load()` from initState, the filter chip, search-submit and
/// mark-all — a notification that arrived while the screen stayed open was
/// never shown until the user left and reopened it, and there was no
/// explicit refresh action either.
void main() {
  Widget buildApp() => ChangeNotifierProvider(
        create: (_) => SessionController(),
        child: MaterialApp(theme: AppTheme.dark, home: const NotificationsScreen()),
      );

  testWidgets('AppBar has an explicit "Osvježi" refresh action', (tester) async {
    await tester.pumpWidget(buildApp());
    // Let the (failing, no backend in the test environment) initial fetch
    // settle without throwing, like reference_data_form_test.dart.
    await tester.pump(const Duration(seconds: 1));

    expect(find.byTooltip('Osvježi'), findsOneWidget);
    expect(find.byIcon(Icons.refresh), findsOneWidget);
  });

  testWidgets('tapping the refresh action does not throw', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.pump(const Duration(seconds: 1));

    await tester.tap(find.byTooltip('Osvježi'));
    await tester.pump(const Duration(seconds: 1));

    expect(tester.takeException(), isNull);
  });

  testWidgets('the 30s background-refresh timer is cancelled when the screen is closed', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.pump(const Duration(seconds: 1));

    // Unmount the screen (e.g. the user pressed Back). If the periodic
    // refresh Timer this screen starts in initState were not cancelled in
    // dispose, flutter_test would fail this test at tearDown with "A Timer
    // is still pending" — a Timer.periodic never completes on its own, so
    // this alone proves dispose() cancels it, without needing to wait a
    // real 30s for it to actually fire.
    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump(const Duration(seconds: 1));

    expect(tester.takeException(), isNull);
  });

  group('background auto-refresh against a fake backend', () {
    // dsk-notifications-stale-poll: the 30s timer used the live
    // TextEditingController text and had no way to tell an in-flight
    // request apart from a newer one — these drive the real screen through
    // ApiClient.instance.dio instead of testing a copy of the logic.
    Map<String, dynamic> item(int id, String title, {required bool read}) =>
        {'id': id, 'title': title, 'body': 'body $id', 'isRead': read, 'createdAt': '2026-01-01T10:00:00Z'};

    testWidgets('a notification that arrives while open shows up after 30s with no full-screen spinner', (tester) async {
      final api = FakeApiInterceptor.install();
      addTearDown(api.uninstall);
      var call = 0;
      api.on('GET', '/api/notifications', (options, handler) {
        call++;
        final items = call == 1
            ? [item(1, 'Prva', read: false)]
            : [item(1, 'Prva', read: false), item(2, 'Nova za vrijeme čekanja', read: false)];
        handler.resolve(fakeResponse(options, items));
      });

      await tester.pumpWidget(buildApp());
      await tester.pump(Duration.zero);
      expect(find.text('Prva'), findsOneWidget);
      expect(find.text('Nova za vrijeme čekanja'), findsNothing);

      await tester.pump(const Duration(seconds: 30));

      expect(find.text('Nova za vrijeme čekanja'), findsOneWidget);
      expect(find.byType(CircularProgressIndicator), findsNothing);
    });

    testWidgets('a stale poll response landing after a filter change does not overwrite the newer list', (tester) async {
      final api = FakeApiInterceptor.install();
      addTearDown(api.uninstall);
      var call = 0;
      RequestInterceptorHandler? staleHandler;
      RequestOptions? staleOptions;
      api.on('GET', '/api/notifications', (options, handler) {
        call++;
        if (call == 1) {
          handler.resolve(fakeResponse(options, [item(1, 'Nepročitana', read: false), item(2, 'Pročitana', read: true)]));
        } else if (call == 2) {
          // The 30s timer's poll — held open, simulating it still being in flight.
          staleOptions = options;
          staleHandler = handler;
        } else {
          // The filter toggle's own foreground load, which answers first.
          handler.resolve(fakeResponse(options, [item(1, 'Nepročitana', read: false)]));
        }
      });

      await tester.pumpWidget(buildApp());
      await tester.pump(Duration.zero);
      expect(find.text('Pročitana'), findsOneWidget);

      // Let the background timer fire; its request is captured, not answered yet.
      await tester.pump(const Duration(seconds: 30));
      expect(staleHandler, isNotNull);

      await tester.tap(find.byType(FilterChip));
      await tester.pump(Duration.zero);
      expect(find.text('Nepročitana'), findsOneWidget);
      expect(find.text('Pročitana'), findsNothing, reason: 'the filtered load answered first and should be on screen');

      // The stale, unfiltered response now arrives — it must be ignored.
      staleHandler!.resolve(fakeResponse(staleOptions!, [item(1, 'Nepročitana', read: false), item(2, 'Pročitana', read: true)]));
      await tester.pump(Duration.zero);

      expect(find.text('Pročitana'), findsNothing, reason: 'a superseded response must not overwrite the current filtered list');
    });

    testWidgets('typing a search without submitting is not applied by the background poll', (tester) async {
      final api = FakeApiInterceptor.install();
      addTearDown(api.uninstall);
      final searches = <String?>[];
      api.on('GET', '/api/notifications', (options, handler) {
        searches.add(options.queryParameters['search'] as String?);
        handler.resolve(fakeResponse(options, <Map<String, dynamic>>[]));
      });

      await tester.pumpWidget(buildApp());
      await tester.pump(Duration.zero);

      await tester.enterText(find.byType(TextField), 'polu');
      await tester.pump(const Duration(seconds: 30));

      expect(searches.last, isNull, reason: 'text typed but never submitted must not reach the background poll');
    });
  });
}
