import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/presentation/widgets/gb_scaffold.dart';

import 'support/fakes.dart';

void main() {
  testWidgets(
      'GbScaffold shows the hamburger (not a back arrow) at the navigation root',
      (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: GbScaffold(
          notificationRepository: FakeNotificationRepository(),
          body: const Center(child: Text('Root screen')),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byIcon(Icons.menu_rounded), findsOneWidget);
    expect(find.byIcon(Icons.arrow_back_rounded), findsNothing);
    expect(find.text('MENU'), findsOneWidget);
  });

  testWidgets(
      'GbScaffold shows an on-screen back arrow (course rule) once pushed on top of '
      'another screen, and tapping it pops back', (tester) async {
    final navigatorKey = GlobalKey<NavigatorState>();

    await tester.pumpWidget(
      MaterialApp(
        navigatorKey: navigatorKey,
        home: Scaffold(
          body: Center(
            child: ElevatedButton(
              onPressed: () => navigatorKey.currentState!.push(
                MaterialPageRoute(
                  builder: (_) => GbScaffold(
                    notificationRepository: FakeNotificationRepository(),
                    title: 'Mentori po vrsti',
                    body: const Center(child: Text('Drill-down screen')),
                  ),
                ),
              ),
              child: const Text('Otvori mentore'),
            ),
          ),
        ),
      ),
    );

    await tester.tap(find.text('Otvori mentore'));
    await tester.pumpAndSettle();

    // Pushed on top of the root route: a back arrow replaces the hamburger,
    // satisfying "Forme moraju omogućiti lako kretanje ... uključujući
    // dugme Back".
    expect(find.byIcon(Icons.arrow_back_rounded), findsOneWidget);
    expect(find.byIcon(Icons.menu_rounded), findsNothing);
    expect(find.text('Mentori po vrsti'), findsOneWidget);
    expect(find.text('Drill-down screen'), findsOneWidget);

    await tester.tap(find.byIcon(Icons.arrow_back_rounded));
    await tester.pumpAndSettle();

    expect(find.text('Otvori mentore'), findsOneWidget);
    expect(find.text('Drill-down screen'), findsNothing);
  });
}
