import 'package:flutter/material.dart';

import '../../../core/network/dio_client.dart';
import '../../../core/theme/app_theme.dart';
import '../../../data/models/training_plan_model.dart';
import '../../../data/repositories/progress_repository.dart';
import '../../widgets/app_panel.dart';

class TrainingDetailScreen extends StatefulWidget {
  const TrainingDetailScreen({super.key, required this.day});

  final PlanDayModel day;

  @override
  State<TrainingDetailScreen> createState() => _TrainingDetailScreenState();
}

class _TrainingDetailScreenState extends State<TrainingDetailScreen> {
  final ProgressRepository _progressRepository = ProgressRepository(DioClient());
  bool _isBusy = false;

  Future<void> _markReviewed() async {
    setState(() => _isBusy = true);
    try {
      await _progressRepository.createProgressEntry({
        'conditioning': 'Reviewed session: ${widget.day.title}',
      });

      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Session review saved to progress history.')),
      );
    } catch (error) {
      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Unable to save review: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _isBusy = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final day = widget.day;

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
              onPressed: _isBusy ? null : _markReviewed,
              child: _isBusy
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Mark as reviewed'),
            ),
          ),
        ],
      ),
    );
  }
}
