import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../demo/mobile_demo_data.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/section_header.dart';
import 'training_detail_screen.dart';

class MyPlanScreen extends StatefulWidget {
  const MyPlanScreen({super.key});

  @override
  State<MyPlanScreen> createState() => _MyPlanScreenState();
}

class _MyPlanScreenState extends State<MyPlanScreen> {
  String _searchQuery = '';

  List<PlanDay> get _filteredDays {
    final normalizedQuery = _searchQuery.trim().toLowerCase();

    return MobileDemoData.planDays.where((day) {
      if (normalizedQuery.isEmpty) {
        return true;
      }

      return day.title.toLowerCase().contains(normalizedQuery) ||
          day.focus.toLowerCase().contains(normalizedQuery) ||
          day.summary.toLowerCase().contains(normalizedQuery);
    }).toList();
  }

  @override
  Widget build(BuildContext context) {
    final plan = MobileDemoData.currentPlan;

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(20, 18, 20, 0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('My Plan', style: Theme.of(context).textTheme.displaySmall),
            const SizedBox(height: 6),
            Text(
              'Motivation, session list and searchable daily structure.',
              style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                    color: AppTheme.textMutedColor,
                  ),
            ),
            const SizedBox(height: 20),
            AppPanel(
              gradient: LinearGradient(
                colors: [
                  AppTheme.accentColor.withValues(alpha: 0.28),
                  AppTheme.surfaceColor,
                ],
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Week ${plan.weekNumber}', style: Theme.of(context).textTheme.labelLarge),
                  const SizedBox(height: 10),
                  Text(plan.focusTitle, style: Theme.of(context).textTheme.headlineMedium),
                  const SizedBox(height: 10),
                  Text('"${plan.motivationalQuote}"'),
                  const SizedBox(height: 16),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(999),
                    child: LinearProgressIndicator(
                      value: plan.completionRate,
                      minHeight: 10,
                      backgroundColor: Colors.white.withValues(alpha: 0.08),
                    ),
                  ),
                  const SizedBox(height: 10),
                  Text(
                    'Next session: ${plan.nextSessionTitle} | ${plan.nextSessionDuration}',
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: AppTheme.textMutedColor,
                        ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),
            TextField(
              onChanged: (value) {
                setState(() {
                  _searchQuery = value;
                });
              },
              decoration: const InputDecoration(
                prefixIcon: Icon(Icons.search_rounded),
                hintText: 'Search sessions by day, focus or summary',
              ),
            ),
            const SizedBox(height: 18),
            SectionHeader(
              title: '${_filteredDays.length} sessions this week',
              subtitle: 'Search is available here as well to satisfy filtered data browsing.',
            ),
            const SizedBox(height: 12),
            Expanded(
              child: ListView.separated(
                itemCount: _filteredDays.length,
                separatorBuilder: (_, __) => const SizedBox(height: 12),
                itemBuilder: (context, index) {
                  final day = _filteredDays[index];

                  return AppPanel(
                    onTap: () {
                      Navigator.of(context).push(
                        MaterialPageRoute<void>(
                          builder: (_) => TrainingDetailScreen(day: day),
                        ),
                      );
                    },
                    color: AppTheme.surfaceColor,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Container(
                              width: 52,
                              height: 52,
                              decoration: BoxDecoration(
                                color: day.recovery
                                    ? AppTheme.secondaryColor.withValues(alpha: 0.14)
                                    : AppTheme.accentColor.withValues(alpha: 0.14),
                                borderRadius: BorderRadius.circular(18),
                              ),
                              child: Center(
                                child: Text(
                                  day.dayLabel,
                                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                                        color: day.recovery ? AppTheme.secondaryColor : AppTheme.accentColor,
                                      ),
                                ),
                              ),
                            ),
                            const SizedBox(width: 14),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(day.title, style: Theme.of(context).textTheme.titleLarge),
                                  const SizedBox(height: 6),
                                  Text(
                                    '${day.focus} | ${day.durationLabel}',
                                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                                          color: AppTheme.textMutedColor,
                                        ),
                                  ),
                                ],
                              ),
                            ),
                            _StatusBadge(
                              label: day.completed
                                  ? 'Done'
                                  : day.recovery
                                      ? 'Recovery'
                                      : 'Queued',
                            ),
                          ],
                        ),
                        const SizedBox(height: 14),
                        Text(day.summary),
                        const SizedBox(height: 14),
                        Text(
                          day.mainBlocks.take(2).join(' | '),
                          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                                color: AppTheme.textMutedColor,
                              ),
                        ),
                      ],
                    ),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Text(label),
    );
  }
}
