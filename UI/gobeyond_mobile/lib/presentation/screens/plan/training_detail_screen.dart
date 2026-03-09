import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../demo/mobile_demo_data.dart';
import '../../widgets/app_panel.dart';

class TrainingDetailScreen extends StatelessWidget {
  const TrainingDetailScreen({super.key, required this.day});

  final PlanDay day;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(day.title)),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 8, 20, 24),
        children: [
          AppPanel(
            gradient: LinearGradient(
              colors: [
                day.recovery
                    ? AppTheme.secondaryColor.withValues(alpha: 0.24)
                    : AppTheme.accentColor.withValues(alpha: 0.24),
                AppTheme.surfaceColor,
              ],
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '${day.dayLabel} | ${day.focus} | ${day.durationLabel}',
                  style: Theme.of(context).textTheme.labelLarge,
                ),
                const SizedBox(height: 10),
                Text(day.summary, style: Theme.of(context).textTheme.bodyLarge),
                const SizedBox(height: 12),
                Text(
                  'Coach note: ${day.coachNote}',
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: AppTheme.textMutedColor,
                      ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 20),
          Text('Session blocks', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 12),
          ...day.mainBlocks.map(
            (block) => Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: AppPanel(
                color: AppTheme.surfaceColor,
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Icon(Icons.drag_indicator_rounded, color: AppTheme.textMutedColor),
                    const SizedBox(width: 10),
                    Expanded(child: Text(block)),
                  ],
                ),
              ),
            ),
          ),
          const SizedBox(height: 10),
          SizedBox(
            width: double.infinity,
            child: ElevatedButton(
              onPressed: () {
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(content: Text('Session marked as reviewed in UI demo.')),
                );
              },
              child: const Text('Mark as reviewed'),
            ),
          ),
        ],
      ),
    );
  }
}
