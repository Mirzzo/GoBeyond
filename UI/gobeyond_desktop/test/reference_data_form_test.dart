import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:gobeyond_desktop/core/theme/app_theme.dart';
import 'package:gobeyond_desktop/presentation/screens/admin/admin_reference_data_screen.dart';

void main() {
  testWidgets('Šifarnici — DODAJ form rejects an empty name before saving', (tester) async {
    await tester.pumpWidget(MaterialApp(theme: AppTheme.dark, home: const Scaffold(body: AdminReferenceDataScreen())));
    // Let the (failing, since there is no backend in the test environment)
    // initial reference-data fetch settle without throwing.
    await tester.pump(const Duration(seconds: 1));

    expect(find.text('VRSTE TRENINGA'), findsOneWidget);

    await tester.tap(find.widgetWithText(ElevatedButton, 'DODAJ'));
    await tester.pumpAndSettle();

    expect(find.text('Dodaj — Vrste treninga'), findsOneWidget);

    await tester.tap(find.widgetWithText(ElevatedButton, 'SAČUVAJ'));
    await tester.pump();

    expect(find.text('Naziv je obavezno.'), findsOneWidget);
  });
}
