import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../demo/mobile_demo_data.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/category_card.dart';
import '../../widgets/section_header.dart';
import '../subscription/subscription_screen.dart';

class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key, required this.onNavigate});

  final ValueChanged<int> onNavigate;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final plan = MobileDemoData.currentPlan;
    final subscription = MobileDemoData.currentSubscription;

    return SafeArea(
      child: CustomScrollView(
        slivers: [
          SliverPadding(
            padding: const EdgeInsets.fromLTRB(20, 18, 20, 24),
            sliver: SliverList(
              delegate: SliverChildListDelegate([
                Text(
                  'GoBeyond',
                  style: theme.textTheme.displaySmall,
                ),
                const SizedBox(height: 6),
                Text(
                  'Your client dashboard for coaching, structure and recovery-aware progress.',
                  style: theme.textTheme.bodyLarge?.copyWith(
                    color: AppTheme.textMutedColor,
                  ),
                ),
                const SizedBox(height: 24),
                AppPanel(
                  gradient: LinearGradient(
                    colors: [
                      AppTheme.accentColor.withValues(alpha: 0.30),
                      const Color(0xFF1B2429),
                      AppTheme.secondaryColor.withValues(alpha: 0.18),
                    ],
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Week ${plan.weekNumber} is underway.',
                        style: theme.textTheme.labelLarge?.copyWith(
                          color: const Color(0xFFFFE8C7),
                        ),
                      ),
                      const SizedBox(height: 10),
                      Text(
                        plan.focusTitle,
                        style: theme.textTheme.headlineMedium,
                      ),
                      const SizedBox(height: 12),
                      Text(plan.focusSummary),
                      const SizedBox(height: 18),
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
                        '${plan.completedSessions}/${plan.totalSessions} sessions completed',
                        style: theme.textTheme.bodyMedium?.copyWith(
                          color: AppTheme.textMutedColor,
                        ),
                      ),
                      const SizedBox(height: 18),
                      Row(
                        children: [
                          Expanded(
                            child: ElevatedButton(
                              onPressed: () => onNavigate(2),
                              child: const Text('Open my plan'),
                            ),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: OutlinedButton(
                              onPressed: () => onNavigate(1),
                              child: const Text('Explore mentors'),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 24),
                const SectionHeader(
                  title: 'Snapshot',
                  subtitle: 'The key numbers you need before your next session.',
                ),
                const SizedBox(height: 12),
                SizedBox(
                  height: 132,
                  child: ListView(
                    scrollDirection: Axis.horizontal,
                    children: const [
                      _MetricCard(
                        title: 'Streak',
                        value: '11 days',
                        helper: 'Daily movement logged',
                        accentColorValue: 0xFFF2A541,
                      ),
                      SizedBox(width: 12),
                      _MetricCard(
                        title: 'Check-in',
                        value: 'Wed',
                        helper: 'Mentor review window',
                        accentColorValue: 0xFF5DD6C0,
                      ),
                      SizedBox(width: 12),
                      _MetricCard(
                        title: 'Next session',
                        value: '58 min',
                        helper: 'Tempo squat focus',
                        accentColorValue: 0xFF8FA8FF,
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 24),
                SectionHeader(
                  title: 'Services',
                  subtitle: 'Core client features required by the mobile flow.',
                  actionLabel: 'Mentors',
                  onAction: () => onNavigate(1),
                ),
                const SizedBox(height: 12),
                ...MobileDemoData.categories.map(
                  (category) => Padding(
                    padding: const EdgeInsets.only(bottom: 12),
                    child: CategoryCard(
                      title: category.title,
                      subtitle: category.subtitle,
                      icon: category.icon,
                      accentColorValue: category.accentColorValue,
                      onTap: () => onNavigate(1),
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                const SectionHeader(
                  title: 'Active subscription',
                  subtitle: 'Status, mentor assignment and renewal timing.',
                ),
                const SizedBox(height: 12),
                AppPanel(
                  color: AppTheme.surfaceColor,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                            decoration: BoxDecoration(
                              color: AppTheme.secondaryColor.withValues(alpha: 0.14),
                              borderRadius: BorderRadius.circular(16),
                            ),
                            child: Text(
                              subscription.status,
                              style: theme.textTheme.labelLarge?.copyWith(
                                color: AppTheme.secondaryColor,
                              ),
                            ),
                          ),
                          const Spacer(),
                          Text(
                            subscription.paymentStatus,
                            style: theme.textTheme.bodyMedium?.copyWith(
                              color: AppTheme.textMutedColor,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 16),
                      Text(subscription.planName, style: theme.textTheme.titleLarge),
                      const SizedBox(height: 6),
                      Text(
                        'Mentor: ${subscription.mentorName}',
                        style: theme.textTheme.bodyLarge,
                      ),
                      const SizedBox(height: 10),
                      Text(
                        '${subscription.progressLabel} | ${subscription.checkInDay} | ${subscription.renewalLabel}',
                        style: theme.textTheme.bodyMedium?.copyWith(
                          color: AppTheme.textMutedColor,
                        ),
                      ),
                      const SizedBox(height: 18),
                      SizedBox(
                        width: double.infinity,
                        child: OutlinedButton(
                          onPressed: () {
                            Navigator.of(context).push(
                              MaterialPageRoute<void>(
                                builder: (_) => const SubscriptionScreen(),
                              ),
                            );
                          },
                          child: const Text('Open subscription'),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 24),
                AppPanel(
                  color: AppTheme.surfaceColor,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Coach note',
                        style: theme.textTheme.titleMedium,
                      ),
                      const SizedBox(height: 10),
                      Text(
                        '"${plan.motivationalQuote}"',
                        style: theme.textTheme.bodyLarge,
                      ),
                      const SizedBox(height: 14),
                      TextButton(
                        onPressed: () => onNavigate(3),
                        child: const Text('Open progress history'),
                      ),
                    ],
                  ),
                ),
              ]),
            ),
          ),
        ],
      ),
    );
  }
}

class _MetricCard extends StatelessWidget {
  const _MetricCard({
    required this.title,
    required this.value,
    required this.helper,
    required this.accentColorValue,
  });

  final String title;
  final String value;
  final String helper;
  final int accentColorValue;

  @override
  Widget build(BuildContext context) {
    final accentColor = Color(accentColorValue);

    return SizedBox(
      width: 164,
      child: AppPanel(
        color: AppTheme.surfaceColor,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: AppTheme.textMutedColor,
                  ),
            ),
            const Spacer(),
            Text(
              value,
              style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                    color: accentColor,
                  ),
            ),
            const SizedBox(height: 8),
            Text(helper),
          ],
        ),
      ),
    );
  }
}
