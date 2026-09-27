import 'package:flutter/material.dart';

import '../../core/theme/app_theme.dart';

/// The large rounded dark panel used throughout every mockup (home card,
/// mentor card, plan card, forms...).
class AppPanel extends StatelessWidget {
  const AppPanel({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(20),
    this.color,
    this.onTap,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final Color? color;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final panel = Container(
      decoration: BoxDecoration(
        color: color ?? AppTheme.panel,
        borderRadius: BorderRadius.circular(AppTheme.panelRadius),
      ),
      padding: padding,
      child: child,
    );

    if (onTap == null) return panel;

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppTheme.panelRadius),
        child: panel,
      ),
    );
  }
}
