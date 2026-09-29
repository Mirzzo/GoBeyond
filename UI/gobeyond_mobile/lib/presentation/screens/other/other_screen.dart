import 'package:flutter/material.dart';

import '../../../core/auth/auth_scope.dart';
import '../../../core/theme/app_theme.dart';
import '../../../data/repositories/notification_repository.dart';
import '../../widgets/app_dialogs.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/gb_scaffold.dart';
import '../auth/login_register_screen.dart';
import '../messages/message_thread_list_screen.dart';
import '../mentor/recommended_mentors_screen.dart';
import '../notifications/notifications_screen.dart';

/// "Ostalo..": Obavijesti, Poruke, Preporučeni mentori and Odjava.
class OtherScreen extends StatefulWidget {
  const OtherScreen({super.key, this.notificationRepository});

  final NotificationRepository? notificationRepository;

  @override
  State<OtherScreen> createState() => _OtherScreenState();
}

class _OtherScreenState extends State<OtherScreen> {
  late final NotificationRepository _notificationRepository =
      widget.notificationRepository ?? ApiNotificationRepository();
  int _unreadCount = 0;

  @override
  void initState() {
    super.initState();
    _loadUnreadCount();
  }

  Future<void> _loadUnreadCount() async {
    try {
      final count = await _notificationRepository.getUnreadCount();
      if (mounted) setState(() => _unreadCount = count);
    } catch (_) {
      // Non-critical; the badge simply stays hidden if this fails.
    }
  }

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
            badgeCount: _unreadCount,
            onTap: () async {
              await Navigator.of(context).push(
                MaterialPageRoute(
                  builder: (_) => NotificationsScreen(
                      notificationRepository: widget.notificationRepository),
                ),
              );
              // Notifications read there lower the unread count.
              _loadUnreadCount();
            },
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
  const _OtherTile({
    required this.icon,
    required this.label,
    required this.onTap,
    this.badgeCount = 0,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final int badgeCount;

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
          if (badgeCount > 0) ...[
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
              decoration: BoxDecoration(
                color: AppTheme.accent,
                borderRadius: BorderRadius.circular(12),
              ),
              child: Text(
                badgeCount > 99 ? '99+' : '$badgeCount',
                style: const TextStyle(
                  color: AppTheme.onAccent,
                  fontWeight: FontWeight.w800,
                  fontSize: 11,
                ),
              ),
            ),
            const SizedBox(width: 10),
          ],
          const Icon(Icons.chevron_right_rounded),
        ],
      ),
    );
  }
}
