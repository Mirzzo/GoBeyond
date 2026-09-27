import 'package:flutter/material.dart';

import '../../core/theme/app_theme.dart';
import '../../data/repositories/notification_repository.dart';
import 'app_drawer.dart';

/// The standard top-level screen shell matching the mobile mockups: a round
/// yellow hamburger button that opens the [AppDrawer] on the six top-level
/// drawer destinations (which always reset the navigation stack to just
/// themselves — see `AppDrawer._open` — so they never end up "poppable"),
/// and a plain back arrow on every drill-down screen reached by pushing on
/// top of one of those, per the course rule that pushed screens must offer
/// an on-screen "Back" control. The drawer stays reachable from drill-down
/// screens too via the standard edge-swipe gesture Flutter attaches
/// whenever a `drawer` is set.
class GbScaffold extends StatelessWidget {
  const GbScaffold({
    super.key,
    required this.body,
    this.title,
    this.actions,
    this.floatingActionButton,
    this.notificationRepository,
  });

  final Widget body;
  final String? title;
  final List<Widget>? actions;
  final Widget? floatingActionButton;

  /// Test seam: lets a test inject a fake so the drawer's unread-count
  /// lookup never hits the real network.
  final NotificationRepository? notificationRepository;

  @override
  Widget build(BuildContext context) {
    final canPop = Navigator.canPop(context);

    return Scaffold(
      drawer: AppDrawer(notificationRepository: notificationRepository),
      appBar: AppBar(
        leading: canPop
            ? IconButton(
                icon: const Icon(Icons.arrow_back_rounded),
                tooltip: 'Nazad',
                onPressed: () => Navigator.of(context).pop(),
              )
            : Builder(
                builder: (ctx) => Padding(
                  padding: const EdgeInsets.only(left: 12),
                  child: Center(
                    child: _HamburgerButton(
                      onTap: () => Scaffold.of(ctx).openDrawer(),
                    ),
                  ),
                ),
              ),
        title: Text(
          canPop ? (title ?? '') : 'MENU',
          style: const TextStyle(
            fontWeight: FontWeight.w800,
            fontSize: 20,
            letterSpacing: 0.5,
          ),
        ),
        actions: actions,
      ),
      body: SafeArea(child: body),
      floatingActionButton: floatingActionButton,
    );
  }
}

class _HamburgerButton extends StatelessWidget {
  const _HamburgerButton({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppTheme.accent,
      shape: const CircleBorder(),
      child: InkWell(
        customBorder: const CircleBorder(),
        onTap: onTap,
        child: const Padding(
          padding: EdgeInsets.all(8),
          child: Icon(Icons.menu_rounded, color: AppTheme.onAccent, size: 22),
        ),
      ),
    );
  }
}
