import 'package:flutter/material.dart';

import '../../../core/auth/auth_scope.dart';
import '../../widgets/app_dialogs.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/gb_scaffold.dart';
import '../auth/login_register_screen.dart';
import '../messages/message_thread_list_screen.dart';
import '../mentor/recommended_mentors_screen.dart';
import '../notifications/notifications_screen.dart';

/// "Ostalo..": Obavijesti, Poruke, Preporučeni mentori and Odjava.
class OtherScreen extends StatelessWidget {
  const OtherScreen({super.key});

  Future<void> _logout(BuildContext context) async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Odjava',
      message: 'Da li ste sigurni da se želite odjaviti?',
      confirmLabel: 'ODJAVI SE',
    );
    if (!confirmed) return;

    await AuthScope.of(context).logout();
    if (!context.mounted) return;
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const LoginRegisterScreen()),
      (route) => false,
    );
  }

  @override
  Widget build(BuildContext context) {
    return GbScaffold(
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          const Text('OSTALO',
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 18)),
          const SizedBox(height: 16),
          _OtherTile(
            icon: Icons.notifications_none_rounded,
            label: 'Obavijesti',
            onTap: () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const NotificationsScreen()),
            ),
          ),
          const SizedBox(height: 14),
          _OtherTile(
            icon: Icons.chat_bubble_outline_rounded,
            label: 'Poruke',
            onTap: () => Navigator.of(context).push(
              MaterialPageRoute(
                  builder: (_) => const MessageThreadListScreen()),
            ),
          ),
          const SizedBox(height: 14),
          _OtherTile(
            icon: Icons.recommend_rounded,
            label: 'Preporučeni mentori',
            onTap: () => Navigator.of(context).push(
              MaterialPageRoute(
                  builder: (_) => const RecommendedMentorsScreen()),
            ),
          ),
          const SizedBox(height: 14),
          _OtherTile(
            icon: Icons.logout_rounded,
            label: 'Odjava',
            onTap: () => _logout(context),
          ),
        ],
      ),
    );
  }
}

class _OtherTile extends StatelessWidget {
  const _OtherTile(
      {required this.icon, required this.label, required this.onTap});

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return AppPanel(
      onTap: onTap,
      child: Row(
        children: [
          Icon(icon, color: Colors.white),
          const SizedBox(width: 16),
          Expanded(
            child: Text(label,
                style:
                    const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
          ),
          const Icon(Icons.chevron_right_rounded),
        ],
      ),
    );
  }
}
