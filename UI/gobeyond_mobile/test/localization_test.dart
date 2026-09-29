import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/navigation/app_navigator.dart';
import 'package:gobeyond_mobile/main.dart';

void main() {
  testWidgets(
      'the real app root renders Material widgets in Bosnian: the date '
      'picker shows "Otkaži", not the English "Cancel"', (tester) async {
    // No stored session, so the app settles on the login screen without
    // touching the network.
    FlutterSecureStorage.setMockInitialValues({});

    await tester.pumpWidget(const GoBeyondApp());
    await tester.pumpAndSettle();

    final context = AppNavigator.key.currentContext!;
    expect(Localizations.localeOf(context), const Locale('bs'));

    showDatePicker(
      context: context,
      initialDate: DateTime(2024, 1, 15),
      firstDate: DateTime(2000),
      lastDate: DateTime(2030),
    );
    await tester.pumpAndSettle();

    expect(find.text('Otkaži'), findsOneWidget);
    expect(find.text('Cancel'), findsNothing);
    expect(find.text('OK'), findsNothing);
  });
}
