import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../widgets/app_panel.dart';
import '../auth/login_register_screen.dart';

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
          child: Padding(
            padding: const EdgeInsets.fromLTRB(24, 20, 24, 24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('GoBeyond', style: theme.textTheme.displaySmall),
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
                        'Sign in, browse mentors, answer a guided questionnaire and track your progress in one flow.',
                        style: theme.textTheme.bodyLarge,
                      ),
                    ],
                  ),
                ),
                const Spacer(),
                SizedBox(
                  width: double.infinity,
                  child: ElevatedButton(
                    onPressed: () {
                      Navigator.of(context).pushReplacement(
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
        ),
      ),
    );
  }
}
