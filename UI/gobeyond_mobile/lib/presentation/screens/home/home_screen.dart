import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../data/models/subscription_model.dart';
import '../../../data/models/training_plan_model.dart';
import '../../../data/repositories/subscription_repository.dart';
import '../../../data/repositories/training_plan_repository.dart';
import '../../../core/network/dio_client.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/category_card.dart';
import '../../widgets/section_header.dart';
import '../subscription/subscription_screen.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key, required this.onNavigate});

  final ValueChanged<int> onNavigate;

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  final TrainingPlanRepository _planRepository = TrainingPlanRepository(DioClient());
  final SubscriptionRepository _subscriptionRepository = SubscriptionRepository(DioClient());

  TrainingPlanModel? _plan;
  SubscriptionModel? _subscription;
  bool _isLoading = true;
  String? _errorMessage;

  static const _categories = [
    ('Strength Plans', 'Weekly structure, deloads and mentor feedback.', Icons.fitness_center_rounded, 0xFFF2A541),
    ('Habit Reset', 'Nutrition, sleep rhythm and realistic adherence.', Icons.track_changes_rounded, 0xFF5DD6C0),
    ('Progress Reviews', 'Monthly metrics, notes and plan changes.', Icons.insights_rounded, 0xFF8FA8FF),
  ];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final subscriptions = await _subscriptionRepository.getMySubscriptions();
      final activeSubscription = subscriptions.cast<SubscriptionModel?>().firstWhere(
            (item) => item?.status == 'Active',
            orElse: () => subscriptions.isEmpty ? null : subscriptions.first,
          );

      TrainingPlanModel? currentPlan;
      try {
        currentPlan = await _planRepository.getCurrentPlan();
      } catch (_) {
        currentPlan = null;
      }

      if (!mounted) {
        return;
      }

      setState(() {
        _subscription = activeSubscription;
        _plan = currentPlan;
      });
    } catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _errorMessage = error.toString();
      });
    } finally {
      if (mounted) {
        setState(() => _isLoading = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final plan = _plan;
    final subscription = _subscription;

    return SafeArea(
      child: RefreshIndicator(
        onRefresh: _load,
        child: CustomScrollView(
          slivers: [
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(20, 18, 20, 24),
              sliver: SliverList(
                delegate: SliverChildListDelegate([
                  Text('GoBeyond', style: theme.textTheme.displaySmall),
                  const SizedBox(height: 6),
                  Text(
                    'Your client dashboard for coaching, structure and recovery-aware progress.',
                    style: theme.textTheme.bodyLarge?.copyWith(
                      color: AppTheme.textMutedColor,
                    ),
                  ),
                  const SizedBox(height: 24),
                  if (_isLoading)
                    const Center(
                      child: Padding(
                        padding: EdgeInsets.all(32),
                        child: CircularProgressIndicator(),
                      ),
                    )
                  else if (_errorMessage != null)
                    AppPanel(
                      color: AppTheme.surfaceColor,
                      child: Text(
                        _errorMessage!,
                        style: theme.textTheme.bodyLarge?.copyWith(color: Colors.redAccent),
                      ),
                    )
                  else ...[
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
                            plan == null ? 'No active plan yet.' : 'Week ${plan.weekNumber} is underway.',
                            style: theme.textTheme.labelLarge?.copyWith(
                              color: const Color(0xFFFFE8C7),
                            ),
                          ),
                          const SizedBox(height: 10),
                          Text(
                            plan?.focusTitle ?? 'Choose a mentor and complete onboarding.',
                            style: theme.textTheme.headlineMedium,
                          ),
                          const SizedBox(height: 12),
                          Text(
                            plan?.focusSummary ??
                                'Browse mentors, answer the questionnaire and confirm your subscription to receive a plan.',
                          ),
                          const SizedBox(height: 18),
                          ClipRRect(
                            borderRadius: BorderRadius.circular(999),
                            child: LinearProgressIndicator(
                              value: plan?.completionRate ?? 0,
                              minHeight: 10,
                              backgroundColor: Colors.white.withValues(alpha: 0.08),
                            ),
                          ),
                          const SizedBox(height: 10),
                          Text(
                            plan == null
                                ? 'No sessions ready'
                                : '${plan.completedSessions}/${plan.totalSessions} sessions completed',
                            style: theme.textTheme.bodyMedium?.copyWith(
                              color: AppTheme.textMutedColor,
                            ),
                          ),
                          const SizedBox(height: 18),
                          Row(
                            children: [
                              Expanded(
                                child: ElevatedButton(
                                  onPressed: () => widget.onNavigate(plan == null ? 1 : 2),
                                  child: Text(plan == null ? 'Explore mentors' : 'Open my plan'),
                                ),
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: OutlinedButton(
                                  onPressed: () => widget.onNavigate(1),
                                  child: const Text('Browse mentors'),
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 24),
                    const SectionHeader(
                      title: 'Services',
                      subtitle: 'Core client features required by the mobile flow.',
                    ),
                    const SizedBox(height: 12),
                    ..._categories.map(
                      (category) => Padding(
                        padding: const EdgeInsets.only(bottom: 12),
                        child: CategoryCard(
                          title: category.$1,
                          subtitle: category.$2,
                          icon: category.$3,
                          accentColorValue: category.$4,
                          onTap: () => widget.onNavigate(1),
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
                                  subscription?.status ?? 'Inactive',
                                  style: theme.textTheme.labelLarge?.copyWith(
                                    color: AppTheme.secondaryColor,
                                  ),
                                ),
                              ),
                              const Spacer(),
                              Text(
                                subscription?.paymentStatus ?? 'No payment yet',
                                style: theme.textTheme.bodyMedium?.copyWith(
                                  color: AppTheme.textMutedColor,
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 16),
                          Text(
                            subscription?.planName ?? 'No subscription selected',
                            style: theme.textTheme.titleLarge,
                          ),
                          const SizedBox(height: 6),
                          Text(
                            'Mentor: ${subscription?.mentorName ?? 'Choose a mentor'}',
                            style: theme.textTheme.bodyLarge,
                          ),
                          const SizedBox(height: 10),
                          Text(
                            subscription == null
                                ? 'Start a subscription to receive a training plan.'
                                : '${subscription.progressLabel} | ${subscription.checkInDay} | ${subscription.renewalLabel}',
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
                    if (plan != null) ...[
                      const SizedBox(height: 24),
                      AppPanel(
                        color: AppTheme.surfaceColor,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text('Coach note', style: theme.textTheme.titleMedium),
                            const SizedBox(height: 10),
                            Text('"${plan.motivationalQuote}"', style: theme.textTheme.bodyLarge),
                            const SizedBox(height: 14),
                            TextButton(
                              onPressed: () => widget.onNavigate(3),
                              child: const Text('Open progress history'),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ],
                ]),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
