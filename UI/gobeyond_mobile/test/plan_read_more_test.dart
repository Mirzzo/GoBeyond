import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/presentation/widgets/truncated_description.dart';

void main() {
  Widget wrap(Widget child) => MaterialApp(
        home: Scaffold(
          body: Center(child: SizedBox(width: 300, child: child)),
        ),
      );

  testWidgets('short OPIS text has no NASTAVI ČITATI button', (tester) async {
    var tapped = false;
    await tester.pumpWidget(
      wrap(TruncatedDescription(
        text: 'Kratak opis treninga za danas.',
        onReadMore: () => tapped = true,
        maxLines: 8,
      )),
    );

    expect(find.textContaining('NASTAVI ČITATI'), findsNothing);
    expect(tapped, isFalse);
  });

  testWidgets(
      'long OPIS text is truncated and shows NASTAVI ČITATI, which opens the full text',
      (tester) async {
    var tapped = false;
    final longText =
        List.generate(20, (i) => 'Ovo je red broj $i sa opisom treninga.')
            .join('\n');

    await tester.pumpWidget(
      wrap(TruncatedDescription(
        text: longText,
        onReadMore: () => tapped = true,
        maxLines: 8,
      )),
    );

    expect(find.textContaining('NASTAVI ČITATI'), findsOneWidget);

    await tester.tap(find.textContaining('NASTAVI ČITATI'));
    await tester.pump();

    expect(tapped, isTrue);
  });

  testWidgets(
      'the full, unmodified text (with original line breaks) is preserved for reading',
      (tester) async {
    const original =
        'Prvi red treninga.\nDrugi red treninga.\n🔥 Emoji ostaje netaknut.';

    await tester.pumpWidget(
      wrap(
          TruncatedDescription(text: original, onReadMore: () {}, maxLines: 8)),
    );

    final textWidget = tester.widget<Text>(find.text(original));
    expect(textWidget.data, original);
  });
}
