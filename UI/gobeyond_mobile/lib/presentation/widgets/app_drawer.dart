import 'package:flutter/material.dart';

import '../../core/theme/app_theme.dart';
import '../../data/repositories/notification_repository.dart';
import '../screens/about/about_screen.dart';
import '../screens/home/home_screen.dart';
import '../screens/other/other_screen.dart';
import '../screens/plan/my_plan_screen.dart';
import '../screens/profile/profile_screen.dart';
import '../screens/subscription/subscription_screen.dart';
import 'gobeyond_logo.dart';

/// The drawer from mockup 13: Moj profil / Početna / Moj plan / Pretplata,
/// the GOBEYOND wordmark, then O nama / Ostalo.. — no bottom navigation bar
/// anywhere in this app.
class AppDrawer extends StatefulWidget {
  const AppDrawer({super.key, this.notificationRepository});

  final NotificationRepository? notificationRepository;

  @override
  State<AppDrawer> createState() => _AppDrawerState();
}

class _AppDrawerState extends State<AppDrawer> {
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

  void _open(BuildContext context, Widget screen) {
    Navigator.of(context).pop();
    // Reset the whole navigation stack to just the chosen destination so a
    // drawer destination is never left "poppable" (which would show a back
    // arrow instead of the hamburger, per GbScaffold) and repeated drawer
    // navigation never leaves duplicate frames behind.
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => screen),
      (route) => false,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Drawer(
      backgroundColor: const Color(0xFF272727),
      child: SafeArea(
        child: ListView(
          padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 20),
          children: [
            const Padding(
              padding: EdgeInsets.only(bottom: 24, left: 4),
              child: Text(
                'MENU',
                style: TextStyle(
                  color: Colors.white,
                  fontSize: 24,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
            _DrawerPill(
              icon: Icons.account_circle_rounded,
              label: 'Moj profil',
              onTap: () => _open(context, const ProfileScreen()),
            ),
            const SizedBox(height: 14),
            _DrawerPill(
              icon: Icons.home_rounded,
              label: 'Početna',
              onTap: () => _open(context, const HomeScreen()),
            ),
            const SizedBox(height: 14),
            _DrawerPill(
              icon: Icons.flag_rounded,
              label: 'Moj plan',
              onTap: () => _open(context, const MyPlanScreen()),
            ),
            const SizedBox(height: 14),
            _DrawerPill(
              icon: Icons.card_membership_rounded,
              label: 'Pretplata',
              onTap: () => _open(context, const SubscriptionScreen()),
            ),
            const SizedBox(height: 40),
            const Center(child: GoBeyondLogo(fontSize: 30)),
            const SizedBox(height: 40),
            _DrawerPill(
              icon: Icons.info_rounded,
              label: 'O nama',
              onTap: () => _open(context, const AboutScreen()),
            ),
            const SizedBox(height: 14),
            _DrawerPill(
              icon: Icons.more_horiz_rounded,
              label: 'Ostalo..',
              badgeCount: _unreadCount,
              onTap: () => _open(context, const OtherScreen()),
            ),
          ],
        ),
      ),
    );
  }
}

class _DrawerPill extends StatelessWidget {
  const _DrawerPill({
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
    return Material(
      color: AppTheme.panelLight,
      borderRadius: BorderRadius.circular(24),
      child: InkWell(
        borderRadius: BorderRadius.circular(24),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
          child: Row(
            children: [
              CircleAvatar(
                radius: 16,
                backgroundColor: const Color(0xFF2E2E2E),
                child: Icon(icon, color: Colors.white, size: 18),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Text(
                  label,
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w700,
                    fontSize: 15,
                  ),
                ),
              ),
              if (badgeCount > 0)
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
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
            ],
          ),
        ),
      ),
    );
  }
}
