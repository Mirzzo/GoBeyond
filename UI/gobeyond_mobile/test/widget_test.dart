import 'package:flutter_test/flutter_test.dart';

import 'package:gobeyond_mobile/main.dart';

void main() {
  testWidgets('renders GoBeyond onboarding shell', (WidgetTester tester) async {
    await tester.pumpWidget(const GoBeyondApp());

    expect(find.text('GoBeyond'), findsOneWidget);
    expect(find.text('Open client app'), findsOneWidget);
  });
}
