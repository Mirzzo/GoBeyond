import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:gobeyond_desktop/core/theme/app_theme.dart';
import 'package:gobeyond_desktop/presentation/screens/mentor/widgets/plan_day_dialog.dart';

void main() {
  Widget buildApp() {
    return MaterialApp(
      theme: AppTheme.dark,
      home: Scaffold(
        body: Builder(
          builder: (context) => ElevatedButton(
            onPressed: () => showPlanDayDialog(
              context,
              planId: 1,
              dayOfWeek: 1,
              dayName: 'Ponedjeljak',
            ),
            child: const Text('open'),
          ),
        ),
      ),
    );
  }

  testWidgets('rejects an empty training description before contacting the API', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();

    expect(find.text('PONEDJELJAK'), findsOneWidget);
    expect(find.text('TRENING'), findsOneWidget);

    await tester.tap(find.widgetWithText(ElevatedButton, 'SPREMI'));
    await tester.pump();

    expect(find.text('Opis treninga mora imati između 10 i 8000 znakova.'), findsOneWidget);
  });

  testWidgets('shows the X close button in the dialog title bar', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();

    expect(find.byIcon(Icons.close), findsOneWidget);
  });
}
