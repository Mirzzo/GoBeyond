import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:gobeyond_desktop/main.dart';

void main() {
  testWidgets('boots desktop app', (WidgetTester tester) async {
    await tester.pumpWidget(const GoBeyondDesktopApp());
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 200));

    expect(find.byType(MaterialApp), findsOneWidget);
    expect(find.byType(Scaffold), findsWidgets);
  });
}
