import 'package:flutter/material.dart';

import '../../core/theme/app_theme.dart';

/// The big yellow rounded button with bold black text seen across every
/// mockup (WEIGHTLIFTING/CALISTHENICS/HYBRID, PRETPLATI SE, POTVRDI, ...).
class PrimaryButton extends StatelessWidget {
  const PrimaryButton({
    super.key,
    required this.label,
    this.onPressed,
    this.icon,
    this.trailing,
    this.isLoading = false,
    this.height = 56,
  });

  final String label;
  final VoidCallback? onPressed;
  final Widget? icon;
  final Widget? trailing;
  final bool isLoading;
  final double height;

  // The theme's vertical padding (16) is tuned for the standard 56 height;
  // at a smaller height it leaves too little room for the label and clips
  // it, so a shorter button gets less vertical padding. The horizontal
  // padding stays the theme's.
  ButtonStyle _shortStyle(BuildContext context) {
    final themePadding = Theme.of(context)
        .elevatedButtonTheme
        .style
        ?.padding
        ?.resolve(const <WidgetState>{})
        ?.resolve(Directionality.of(context));
    return ElevatedButton.styleFrom(
      padding: EdgeInsets.fromLTRB(
          themePadding?.left ?? 0, 8, themePadding?.right ?? 0, 8),
    );
  }

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: double.infinity,
      height: height,
      child: ElevatedButton(
        onPressed: isLoading ? null : onPressed,
        style: height < 56 ? _shortStyle(context) : null,
        child: isLoading
            ? const SizedBox(
                width: 22,
                height: 22,
                child: CircularProgressIndicator(
                  strokeWidth: 2.6,
                  color: AppTheme.onAccent,
                ),
              )
            : Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  if (icon != null) ...[icon!, const SizedBox(width: 10)],
                  Flexible(
                    child: Text(
                      label,
                      textAlign: TextAlign.center,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                  if (trailing != null) ...[
                    const SizedBox(width: 10),
                    trailing!,
                  ],
                ],
              ),
      ),
    );
  }
}
