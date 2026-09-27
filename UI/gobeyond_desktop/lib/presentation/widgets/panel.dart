import 'package:flutter/material.dart';

import '../../core/theme/app_theme.dart';

/// The big rounded content panel used on every screen (mockups 01/02/03/06):
/// a darker header bar with the yellow screen title, then the body.
class ContentPanel extends StatelessWidget {
  const ContentPanel({super.key, required this.title, required this.child, this.headerActions});

  final String title;
  final Widget child;
  final List<Widget>? headerActions;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(color: AppColors.panel, borderRadius: BorderRadius.circular(20)),
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 18),
            color: AppColors.panelLight,
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    title,
                    textAlign: TextAlign.center,
                    style: const TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 20),
                  ),
                ),
                if (headerActions != null) ...headerActions!,
              ],
            ),
          ),
          Expanded(child: Padding(padding: const EdgeInsets.all(20), child: child)),
        ],
      ),
    );
  }
}

/// Search input with a magnifier icon (course rule: every list needs at
/// least one search parameter).
class SearchField extends StatelessWidget {
  const SearchField({
    super.key,
    required this.controller,
    this.hintText = 'Pretraga',
    this.onChanged,
    this.onSubmitted,
  });

  final TextEditingController controller;
  final String hintText;
  final ValueChanged<String>? onChanged;
  final ValueChanged<String>? onSubmitted;

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: controller,
      onChanged: onChanged,
      onSubmitted: onSubmitted,
      style: const TextStyle(color: Colors.white),
      decoration: InputDecoration(
        hintText: hintText,
        hintStyle: const TextStyle(color: AppColors.textMuted),
        suffixIcon: const Icon(Icons.search, color: Colors.white),
        filled: true,
        fillColor: AppColors.panelLight,
      ),
    );
  }
}

/// A dark pill-shaped secondary button (mockup buttons like PREGLED.., UREDI).
class PillButton extends StatelessWidget {
  const PillButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.icon,
    this.dense = false,
    this.color,
  });

  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;
  final bool dense;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    return ElevatedButton(
      onPressed: onPressed,
      style: ElevatedButton.styleFrom(
        backgroundColor: color ?? const Color(0xFF1E1E1E),
        foregroundColor: Colors.white,
        disabledBackgroundColor: const Color(0xFF1E1E1E).withValues(alpha: 0.5),
        disabledForegroundColor: Colors.white38,
        padding: EdgeInsets.symmetric(horizontal: dense ? 14 : 20, vertical: dense ? 8 : 12),
        textStyle: TextStyle(fontWeight: FontWeight.bold, fontSize: dense ? 12 : 13),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(24)),
        elevation: 0,
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) ...[Icon(icon, size: 16), const SizedBox(width: 6)],
          Text(label),
        ],
      ),
    );
  }
}

/// Colored status chip; pass a Bosnian label + explicit color per status.
class StatusChip extends StatelessWidget {
  const StatusChip({super.key, required this.label, required this.color});

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.2),
        border: Border.all(color: color),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(label, style: TextStyle(color: color, fontWeight: FontWeight.bold, fontSize: 12)),
    );
  }
}

class SubscriptionStatusPresentation {
  const SubscriptionStatusPresentation._();

  static const Map<String, String> _labels = {
    'PendingPayment': 'Čeka plaćanje',
    'AwaitingMentor': 'Čeka mentora',
    'Active': 'Aktivna',
    'Rejected': 'Odbijena',
    'Cancelled': 'Otkazana',
    'Expired': 'Istekla',
  };

  static const Map<String, Color> _colors = {
    'PendingPayment': Color(0xFFE0A64F),
    'AwaitingMentor': AppColors.info,
    'Active': AppColors.success,
    'Rejected': AppColors.danger,
    'Cancelled': Color(0xFF9E9E9E),
    'Expired': Color(0xFF9E9E9E),
  };

  static String label(String status) => _labels[status] ?? status;
  static Color color(String status) => _colors[status] ?? AppColors.textMuted;
}

class PlanStatusPresentation {
  const PlanStatusPresentation._();

  static const Map<String, String> _labels = {
    'Draft': 'Nacrt',
    'Published': 'Objavljen',
    'Archived': 'Arhiviran',
  };

  static const Map<String, Color> _colors = {
    'Draft': Color(0xFFE0A64F),
    'Published': AppColors.success,
    'Archived': Color(0xFF9E9E9E),
  };

  static String label(String status) => _labels[status] ?? status;
  static Color color(String status) => _colors[status] ?? AppColors.textMuted;
}

/// Small labeled stat tile used on POČETNA / KONTROLNA PLOČA overview cards.
class StatTile extends StatelessWidget {
  const StatTile({super.key, required this.label, required this.value, this.icon});

  final String label;
  final String value;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      constraints: const BoxConstraints(minWidth: 190),
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(16)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) Icon(icon, color: AppColors.accent, size: 22),
          if (icon != null) const SizedBox(height: 8),
          Text(value, style: const TextStyle(color: Colors.white, fontSize: 24, fontWeight: FontWeight.bold)),
          const SizedBox(height: 4),
          Text(label, style: const TextStyle(color: AppColors.textMuted, fontSize: 13)),
        ],
      ),
    );
  }
}

class EmptyState extends StatelessWidget {
  const EmptyState({super.key, required this.message});
  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Text(message, style: const TextStyle(color: AppColors.textMuted, fontSize: 15)),
      ),
    );
  }
}
