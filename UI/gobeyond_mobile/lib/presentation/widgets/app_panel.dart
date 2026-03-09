import 'package:flutter/material.dart';

import '../../core/theme/app_theme.dart';

class AppPanel extends StatelessWidget {
  const AppPanel({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(20),
    this.color,
    this.gradient,
    this.onTap,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final Color? color;
  final Gradient? gradient;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    const radius = 28.0;

    final panel = Ink(
      decoration: BoxDecoration(
        color: color ?? AppTheme.panelColor,
        gradient: gradient,
        borderRadius: BorderRadius.circular(radius),
        border: Border.all(color: const Color(0xFF2B363C)),
        boxShadow: const [
          BoxShadow(
            color: Color(0x22000000),
            blurRadius: 28,
            offset: Offset(0, 14),
          ),
        ],
      ),
      child: Padding(
        padding: padding,
        child: child,
      ),
    );

    return Material(
      color: Colors.transparent,
      child: onTap == null
          ? panel
          : InkWell(
              onTap: onTap,
              borderRadius: BorderRadius.circular(radius),
              child: panel,
            ),
    );
  }
}
