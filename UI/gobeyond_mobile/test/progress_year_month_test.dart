import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/presentation/screens/progress/training_history_screen.dart';

import 'support/fakes.dart';

void main() {
  testWidgets(
      'Historija treninga lets the user pick GODINA and MJESEC, re-fetching the entry each time',
      (tester) async {
    final repository = FakeProgressRepository();

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: TrainingHistoryScreen(progressRepository: repository),
      ),
    );
    await tester.pumpAndSettle();

    // The fake only reports 2024/2025 as available years, so the screen
    // falls back to the first of those instead of the real current year.
    expect(find.textContaining('GODINA: 2024'), findsOneWidget);

    // Pick a different year from the GODINA bottom sheet.
    await tester.tap(find.textContaining('GODINA: 2024'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('2025').last);
    await tester.pumpAndSettle();

    expect(find.textContaining('GODINA: 2025'), findsOneWidget);
    expect(repository.getEntryCalls.contains('2025-${DateTime.now().month}'),
        isTrue);

    // Pick a specific month from the MJESEC bottom sheet.
    await tester.tap(find.textContaining('MJESEC:'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Mart').last);
    await tester.pumpAndSettle();

    expect(find.textContaining('MJESEC: Mart'), findsOneWidget);
    expect(repository.getEntryCalls.contains('2025-3'), isTrue);
  });
}
