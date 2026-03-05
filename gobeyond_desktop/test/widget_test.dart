import 'package:flutter_test/flutter_test.dart';

import 'package:gobeyond_desktop/main.dart';

void main() {
  testWidgets('shows GoBeyond login title', (WidgetTester tester) async {
    await tester.pumpWidget(const GoBeyondDesktopApp());
    await tester.pumpAndSettle();

    expect(find.text('GoBeyond'), findsOneWidget);
  });
}
