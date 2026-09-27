import 'package:flutter/material.dart';

import 'primary_button.dart';

/// Renders [text] truncated to [maxLines], preserving the original line
/// breaks/emojis (never re-splitting or reformatting the mentor's text), and
/// only shows the "NASTAVI ČITATI...." button (mockup 12) when the text
/// actually overflows that many lines.
class TruncatedDescription extends StatelessWidget {
  const TruncatedDescription({
    super.key,
    required this.text,
    required this.onReadMore,
    this.maxLines = 8,
    this.style = const TextStyle(height: 1.5),
  });

  final String text;
  final VoidCallback onReadMore;
  final int maxLines;
  final TextStyle style;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final painter = TextPainter(
          text: TextSpan(text: text, style: style),
          maxLines: maxLines,
          textDirection: TextDirection.ltr,
        )..layout(maxWidth: constraints.maxWidth);
        final overflows = painter.didExceedMaxLines;

        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              text,
              style: style,
              maxLines: overflows ? maxLines : null,
              overflow:
                  overflows ? TextOverflow.ellipsis : TextOverflow.visible,
            ),
            if (overflows) ...[
              const SizedBox(height: 14),
              Center(
                child: SizedBox(
                  width: 230,
                  child: PrimaryButton(
                    label: 'NASTAVI ČITATI....',
                    height: 46,
                    onPressed: onReadMore,
                  ),
                ),
              ),
            ],
          ],
        );
      },
    );
  }
}
