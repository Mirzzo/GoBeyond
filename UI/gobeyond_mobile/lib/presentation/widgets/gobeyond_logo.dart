import 'package:flutter/material.dart';

import '../../core/theme/app_theme.dart';

/// The yellow "GOBEYOND" wordmark used on the home panel, drawer and splash
/// screen. The mockups use a script/italic font; without a bundled custom
/// font asset this approximates it with bold italic styling in the brand
/// yellow.
class GoBeyondLogo extends StatelessWidget {
  const GoBeyondLogo({super.key, this.fontSize = 34});

  final double fontSize;

  @override
  Widget build(BuildContext context) {
    return Text(
      'GOBEYOND',
      style: TextStyle(
        color: AppTheme.accent,
        fontSize: fontSize,
        fontWeight: FontWeight.w800,
        fontStyle: FontStyle.italic,
        letterSpacing: 1.2,
        fontFamily: 'cursive',
      ),
    );
  }
}
