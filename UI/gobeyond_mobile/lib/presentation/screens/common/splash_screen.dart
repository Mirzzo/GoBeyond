import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../demo/mobile_demo_data.dart';
import '../../widgets/app_panel.dart';
import '../auth/login_register_screen.dart';
import 'client_shell_screen.dart';

class SplashScreen extends StatelessWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      body: Container(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            colors: [
              Color(0xFF0E1214),
              Color(0xFF131A1F),
              Color(0xFF20180D),
            ],
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
          ),
        ),
        child: SafeArea(
          child: LayoutBuilder(
            builder: (context, constraints) {
              return SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(24, 20, 24, 24),
                child: ConstrainedBox(
                  constraints: BoxConstraints(minHeight: constraints.maxHeight),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'GoBeyond',
                        style: theme.textTheme.displaySmall,
                      ),
                      const SizedBox(height: 10),
                      Text(
                        'Client app for structured coaching, progress tracking and profile control.',
                        style: theme.textTheme.bodyLarge?.copyWith(
                          color: AppTheme.textMutedColor,
                        ),
                      ),
                      const SizedBox(height: 28),
                      AppPanel(
                        gradient: LinearGradient(
                          colors: [
                            AppTheme.accentColor.withValues(alpha: 0.28),
                            const Color(0xFF1B2328),
                          ],
                          begin: Alignment.topLeft,
                          end: Alignment.bottomRight,
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              'Train with structure even when your week is not perfect.',
                              style: theme.textTheme.headlineMedium,
                            ),
                            const SizedBox(height: 14),
                            Text(
                              'Browse mentors, answer a guided questionnaire, track adherence and edit your client profile in one flow.',
                              style: theme.textTheme.bodyLarge,
                            ),
                            const SizedBox(height: 20),
                            Wrap(
                              spacing: 10,
                              runSpacing: 10,
                              children: const [
                                _FeatureBadge(label: 'Mentor browse'),
                                _FeatureBadge(label: 'Subscription flow'),
                                _FeatureBadge(label: 'Progress history'),
                                _FeatureBadge(label: 'Profile edit'),
                              ],
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 22),
                      ...MobileDemoData.categories.map((category) {
                        final accentColor = Color(category.accentColorValue);

                        return Padding(
                          padding: const EdgeInsets.only(bottom: 12),
                          child: AppPanel(
                            color: AppTheme.surfaceColor,
                            child: Row(
                              children: [
                                CircleAvatar(
                                  radius: 24,
                                  backgroundColor: accentColor.withValues(alpha: 0.18),
                                  child: Icon(category.icon, color: accentColor),
                                ),
                                const SizedBox(width: 14),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        category.title,
                                        style: theme.textTheme.titleMedium,
                                      ),
                                      const SizedBox(height: 6),
                                      Text(
                                        category.subtitle,
                                        style: theme.textTheme.bodyMedium?.copyWith(
                                          color: AppTheme.textMutedColor,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              ],
                            ),
                          ),
                        );
                      }),
                      const SizedBox(height: 14),
                      SizedBox(
                        width: double.infinity,
                        child: ElevatedButton(
                          onPressed: () {
                            Navigator.of(context).pushReplacement(
                              MaterialPageRoute<void>(
                                builder: (_) => const ClientShellScreen(),
                              ),
                            );
                          },
                          child: const Text('Open client app'),
                        ),
                      ),
                      const SizedBox(height: 12),
                      SizedBox(
                        width: double.infinity,
                        child: OutlinedButton(
                          onPressed: () {
                            Navigator.of(context).push(
                              MaterialPageRoute<void>(
                                builder: (_) => const LoginRegisterScreen(),
                              ),
                            );
                          },
                          child: const Text('Sign in / register'),
                        ),
                      ),
                    ],
                  ),
                ),
              );
            },
          ),
        ),
      ),
    );
  }
}

class _FeatureBadge extends StatelessWidget {
  const _FeatureBadge({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.white.withValues(alpha: 0.08)),
      ),
      child: Text(label),
    );
  }
}
