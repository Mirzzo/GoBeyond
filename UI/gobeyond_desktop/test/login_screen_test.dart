import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:gobeyond_desktop/core/session/session_controller.dart';
import 'package:gobeyond_desktop/core/theme/app_theme.dart';
import 'package:gobeyond_desktop/presentation/screens/login_screen.dart';

void main() {
  Widget buildApp() {
    return ChangeNotifierProvider(
      create: (_) => SessionController(),
      child: MaterialApp(theme: AppTheme.dark, home: const LoginScreen()),
    );
  }

  testWidgets('shows Bosnian validation messages for empty fields without calling the API', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.pumpAndSettle();

    expect(find.text('GOBEYOND'), findsOneWidget);

    await tester.tap(find.widgetWithText(ElevatedButton, 'PRIJAVA'));
    await tester.pump();

    expect(find.text('Unesite korisničko ime ili email.'), findsOneWidget);
    expect(find.text('Unesite lozinku.'), findsOneWidget);
  });

  testWidgets('has a link to mentor registration', (tester) async {
    await tester.pumpWidget(buildApp());
    await tester.pumpAndSettle();

    expect(find.text('Registruj se kao mentor'), findsOneWidget);
  });
}
