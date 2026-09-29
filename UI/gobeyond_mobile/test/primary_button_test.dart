import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/presentation/widgets/primary_button.dart';

void main() {
  Future<EdgeInsetsGeometry?> resolvedPadding(
      WidgetTester tester, double height) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: Scaffold(
          body: PrimaryButton(label: 'VIŠE INFO...', height: height),
        ),
      ),
    );
    final button = tester.widget<ElevatedButton>(find.byType(ElevatedButton));
    return button.style?.padding?.resolve({}) ??
        Theme.of(tester.element(find.byType(ElevatedButton)))
            .elevatedButtonTheme
            .style
            ?.padding
            ?.resolve({});
  }

  testWidgets(
      'a short PrimaryButton (height < 56) gets reduced vertical padding so '
      'the label is not clipped', (tester) async {
    final padding = await resolvedPadding(tester, 40);
    expect(padding, isNotNull);
    expect(padding!.vertical, lessThan(16 * 2));
  });

  testWidgets('a full-height PrimaryButton keeps the theme default padding',
      (tester) async {
    final padding = await resolvedPadding(tester, 56);
    expect(padding, isNotNull);
    expect(padding!.vertical, 16 * 2);
  });

  testWidgets(
      'a short PrimaryButton keeps the theme horizontal padding (only its '
      'vertical padding is reduced)', (tester) async {
    final padding = await resolvedPadding(tester, 40);
    final themePadding = AppTheme.theme.elevatedButtonTheme.style!.padding!
        .resolve({})!;
    expect(themePadding.horizontal, greaterThan(0));
    expect(padding!.horizontal, themePadding.horizontal);
  });
}
