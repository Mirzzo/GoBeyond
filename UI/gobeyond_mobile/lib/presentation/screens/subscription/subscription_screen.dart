import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../data/models/mentor_model.dart';
import '../../demo/mobile_demo_data.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/section_header.dart';

class SubscriptionScreen extends StatelessWidget {
  const SubscriptionScreen({super.key, this.mentor});

  final MentorModel? mentor;

  void _showMessage(BuildContext context, String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message)),
    );
  }

  Future<void> _confirmCancel(BuildContext context) async {
    final shouldCancel = await showDialog<bool>(
          context: context,
          builder: (context) {
            return AlertDialog(
              title: const Text('Cancel subscription?'),
              content: const Text(
                'This is an irreversible action in the current demo flow. You will need to re-subscribe to continue with the mentor.',
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.of(context).pop(false),
                  child: const Text('Keep it'),
                ),
                ElevatedButton(
                  onPressed: () => Navigator.of(context).pop(true),
                  child: const Text('Cancel plan'),
                ),
              ],
            );
          },
        ) ??
        false;

    if (!context.mounted || !shouldCancel) {
      return;
    }

    _showMessage(context, 'Cancellation confirmed in UI demo.');
  }

  @override
  Widget build(BuildContext context) {
    final subscription = MobileDemoData.currentSubscription;
    final mentorName = mentor?.name ?? subscription.mentorName;

    return Scaffold(
      appBar: AppBar(title: const Text('Subscription')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 8, 20, 24),
        children: [
          AppPanel(
            gradient: LinearGradient(
              colors: [
                AppTheme.accentColor.withValues(alpha: 0.26),
                AppTheme.surfaceColor,
              ],
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                  decoration: BoxDecoration(
                    color: AppTheme.secondaryColor.withValues(alpha: 0.14),
                    borderRadius: BorderRadius.circular(16),
                  ),
                  child: Text(
                    subscription.status,
                    style: Theme.of(context).textTheme.labelLarge?.copyWith(
                          color: AppTheme.secondaryColor,
                        ),
                  ),
                ),
                const SizedBox(height: 16),
                Text(
                  subscription.planName,
                  style: Theme.of(context).textTheme.headlineMedium,
                ),
                const SizedBox(height: 10),
                Text(
                  'Assigned mentor: $mentorName',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 10),
                Text(
                  '${subscription.paymentStatus} | ${subscription.renewalLabel} | ${subscription.checkInDay}',
                ),
                const SizedBox(height: 18),
                Wrap(
                  spacing: 10,
                  runSpacing: 10,
                  children: const [
                    _SubscriptionTag(label: 'Payment confirmed'),
                    _SubscriptionTag(label: 'Questionnaire received'),
                    _SubscriptionTag(label: 'Plan active'),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(height: 24),
          const SectionHeader(
            title: 'Included',
            subtitle: 'The UI surfaces what the client gets after subscribing.',
          ),
          const SizedBox(height: 12),
          const AppPanel(
            color: AppTheme.surfaceColor,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _ChecklistItem(label: 'Weekly mentor check-in with plan adjustment'),
                SizedBox(height: 12),
                _ChecklistItem(label: 'Session structure with recovery-aware pacing'),
                SizedBox(height: 12),
                _ChecklistItem(label: 'Progress review and adherence notes'),
              ],
            ),
          ),
          const SizedBox(height: 24),
          const SectionHeader(
            title: 'Status timeline',
            subtitle: 'Payment status and subscription steps are visible in one place.',
          ),
          const SizedBox(height: 12),
          const _TimelineTile(
            title: 'Payment processed',
            subtitle: 'Card payment placeholder confirmed in UI flow.',
          ),
          const SizedBox(height: 10),
          const _TimelineTile(
            title: 'Questionnaire reviewed',
            subtitle: 'Mentor receives your answers before finalizing focus.',
          ),
          const SizedBox(height: 10),
          const _TimelineTile(
            title: 'Week 1 ready',
            subtitle: 'Initial plan block is prepared after onboarding.',
          ),
          const SizedBox(height: 24),
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: () => _showMessage(context, 'Mentor messaging is pending backend integration.'),
                  child: const Text('Message mentor'),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: ElevatedButton(
                  onPressed: () => _showMessage(context, 'Billing management placeholder opened.'),
                  child: const Text('Manage billing'),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            child: OutlinedButton(
              onPressed: () => _confirmCancel(context),
              child: const Text('Cancel subscription'),
            ),
          ),
        ],
      ),
    );
  }
}

class _SubscriptionTag extends StatelessWidget {
  const _SubscriptionTag({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Text(label),
    );
  }
}

class _ChecklistItem extends StatelessWidget {
  const _ChecklistItem({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Padding(
          padding: EdgeInsets.only(top: 2),
          child: Icon(Icons.check_circle_rounded, color: AppTheme.secondaryColor),
        ),
        const SizedBox(width: 12),
        Expanded(child: Text(label)),
      ],
    );
  }
}

class _TimelineTile extends StatelessWidget {
  const _TimelineTile({
    required this.title,
    required this.subtitle,
  });

  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    return AppPanel(
      color: AppTheme.surfaceColor,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            margin: const EdgeInsets.only(top: 2),
            width: 12,
            height: 12,
            decoration: const BoxDecoration(
              color: AppTheme.accentColor,
              shape: BoxShape.circle,
            ),
          ),
          const SizedBox(width: 12),
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
        ],
      ),
    );
  }
}
