import 'package:flutter/material.dart';

import '../../core/theme/app_theme.dart';
import 'app_panel.dart';

class CategoryCard extends StatelessWidget {
  const CategoryCard({
    super.key,
    required this.title,
    required this.subtitle,
    required this.icon,
    required this.accentColorValue,
    this.onTap,
  });

  final String title;
  final String subtitle;
  final IconData icon;
  final int accentColorValue;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final accentColor = Color(accentColorValue);

    return AppPanel(
      onTap: onTap,
      gradient: LinearGradient(
        colors: [
          accentColor.withValues(alpha: 0.22),
          AppTheme.panelColor,
        ],
        begin: Alignment.topLeft,
        end: Alignment.bottomRight,
      ),
      child: Row(
        children: [
          Container(
            width: 52,
            height: 52,
            decoration: BoxDecoration(
              color: accentColor.withValues(alpha: 0.18),
              borderRadius: BorderRadius.circular(18),
            ),
            child: Icon(icon, color: accentColor),
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 6),
                Text(
                  subtitle,
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: AppTheme.textMutedColor,
                      ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 12),
          Icon(
            Icons.arrow_forward_rounded,
            color: accentColor,
          ),
        ],
      ),
    );
  }
}
