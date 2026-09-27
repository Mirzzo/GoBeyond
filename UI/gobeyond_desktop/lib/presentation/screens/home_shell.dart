import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/session/session_controller.dart';
import '../../core/theme/app_theme.dart';
import 'admin/admin_announcements_screen.dart';
import 'admin/admin_clients_screen.dart';
import 'admin/admin_dashboard_screen.dart';
import 'admin/admin_mentor_requests_screen.dart';
import 'admin/admin_mentors_screen.dart';
import 'admin/admin_reference_data_screen.dart';
import 'admin/admin_subscriptions_screen.dart';
import 'admin/admin_users_screen.dart';
import 'mentor/mentor_collaboration_requests_screen.dart';
import 'mentor/mentor_dashboard_screen.dart';
import 'mentor/mentor_messages_screen.dart';
import 'mentor/mentor_published_plans_screen.dart';
import 'mentor/mentor_subscribers_screen.dart';
import 'notifications_screen.dart';
import 'pocetna_screen.dart';
import 'profile_screen.dart';

class _NavItem {
  const _NavItem(this.key, this.label, this.builder);
  final String key;
  final String label;
  final WidgetBuilder builder;
}

/// Persistent shell shown after login: top bar (logo, POČETNA / KONTROLNA
/// PLOČA, notification bell, user menu) + left "UPRAVLJANJE" role menu +
/// content area. Matches mockups 01-03/06.
class HomeShell extends StatefulWidget {
  const HomeShell({super.key});

  @override
  State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell> {
  String _activeKey = 'home';

  List<_NavItem> _sections(SessionController session) {
    if (session.isAdmin) {
      return [
        const _NavItem('admin_requests', 'ZAHTJEVI ZA MENTORA', _buildAdminRequests),
        const _NavItem('admin_mentors', 'MENTORI', _buildAdminMentors),
        const _NavItem('admin_subscriptions', 'UPRAVLJANJE PRETPLATAMA', _buildAdminSubscriptions),
        const _NavItem('admin_clients', 'KLIJENTI', _buildAdminClients),
        const _NavItem('admin_users', 'KORISNICI', _buildAdminUsers),
        const _NavItem('admin_reference', 'ŠIFARNICI', _buildAdminReference),
        const _NavItem('admin_announcements', 'SISTEMSKE OBAVIJESTI', _buildAdminAnnouncements),
      ];
    }
    return [
      const _NavItem('mentor_requests', 'ZAHTJEVI ZA SURADNJU', _buildMentorRequests),
      const _NavItem('mentor_plans', 'IZRAĐENI PLANOVI', _buildMentorPlans),
      const _NavItem('mentor_subscribers', 'PRETPLATNICI', _buildMentorSubscribers),
      const _NavItem('mentor_messages', 'PORUKE', _buildMentorMessages),
    ];
  }

  static Widget _buildAdminRequests(BuildContext context) => const AdminMentorRequestsScreen();
  static Widget _buildAdminMentors(BuildContext context) => const AdminMentorsScreen();
  static Widget _buildAdminSubscriptions(BuildContext context) => const AdminSubscriptionsScreen();
  static Widget _buildAdminClients(BuildContext context) => const AdminClientsScreen();
  static Widget _buildAdminUsers(BuildContext context) => const AdminUsersScreen();
  static Widget _buildAdminReference(BuildContext context) => const AdminReferenceDataScreen();
  static Widget _buildAdminAnnouncements(BuildContext context) => const AdminAnnouncementsScreen();

  static Widget _buildMentorRequests(BuildContext context) => const MentorCollaborationRequestsScreen();
  static Widget _buildMentorPlans(BuildContext context) => const MentorPublishedPlansScreen();
  static Widget _buildMentorSubscribers(BuildContext context) => const MentorSubscribersScreen();
  static Widget _buildMentorMessages(BuildContext context) => const MentorMessagesScreen();

  @override
  Widget build(BuildContext context) {
    final session = context.watch<SessionController>();
    final sections = _sections(session);
    final activeSection = sections.where((s) => s.key == _activeKey).toList();

    Widget content;
    if (_activeKey == 'home') {
      content = const PocetnaScreen();
    } else if (_activeKey == 'dashboard') {
      content = session.isAdmin ? const AdminDashboardScreen() : const MentorDashboardScreen();
    } else if (activeSection.isNotEmpty) {
      content = Builder(builder: activeSection.first.builder);
    } else {
      content = const PocetnaScreen();
    }

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Column(
          children: [
            _TopBar(
              activeKey: _activeKey,
              onSelectHome: () => setState(() => _activeKey = 'home'),
              onSelectDashboard: () => setState(() => _activeKey = 'dashboard'),
            ),
            const Divider(height: 1, color: Colors.white24),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    _LeftMenu(
                      items: sections,
                      activeKey: _activeKey,
                      onSelect: (key) => setState(() => _activeKey = key),
                    ),
                    const SizedBox(width: 20),
                    Expanded(child: content),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar({required this.activeKey, required this.onSelectHome, required this.onSelectDashboard});

  final String activeKey;
  final VoidCallback onSelectHome;
  final VoidCallback onSelectDashboard;

  @override
  Widget build(BuildContext context) {
    final session = context.watch<SessionController>();
    final user = session.user;

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
      child: Row(
        children: [
          const Text(
            'GOBEYOND',
            style: TextStyle(
              color: AppColors.accent,
              fontSize: 26,
              fontWeight: FontWeight.bold,
              fontStyle: FontStyle.italic,
            ),
          ),
          const Spacer(),
          _TopNavButton(label: 'POČETNA', active: activeKey == 'home', onTap: onSelectHome),
          const SizedBox(width: 32),
          _TopNavButton(label: 'KONTROLNA PLOČA', active: activeKey == 'dashboard', onTap: onSelectDashboard),
          const Spacer(),
          IconButton(
            tooltip: 'Obavijesti',
            onPressed: () {
              Navigator.of(context).push(MaterialPageRoute(builder: (_) => const NotificationsScreen()));
            },
            icon: Badge(
              label: Text('${session.unreadCount}'),
              isLabelVisible: session.unreadCount > 0,
              backgroundColor: AppColors.danger,
              child: const Icon(Icons.notifications_outlined, color: Colors.white),
            ),
          ),
          const SizedBox(width: 8),
          PopupMenuButton<String>(
            color: AppColors.panelLight,
            offset: const Offset(0, 46),
            onSelected: (value) async {
              if (value == 'profile') {
                Navigator.of(context).push(MaterialPageRoute(builder: (_) => const ProfileScreen()));
              } else if (value == 'logout') {
                await session.logout();
              }
            },
            itemBuilder: (context) => const [
              PopupMenuItem(value: 'profile', child: Text('Moj profil', style: TextStyle(color: Colors.white))),
              PopupMenuItem(value: 'logout', child: Text('Odjava', style: TextStyle(color: Colors.white))),
            ],
            child: Row(
              children: [
                Text(
                  user?.fullName ?? '',
                  style: const TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold),
                ),
                const SizedBox(width: 8),
                const CircleAvatar(radius: 16, backgroundColor: Colors.white, child: Icon(Icons.person, color: Colors.black87, size: 18)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _TopNavButton extends StatelessWidget {
  const _TopNavButton({required this.label, required this.active, required this.onTap});

  final String label;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Text(
        label,
        style: TextStyle(
          color: active ? Colors.white : AppColors.accent,
          fontWeight: FontWeight.bold,
          fontSize: 16,
        ),
      ),
    );
  }
}

class _LeftMenu extends StatelessWidget {
  const _LeftMenu({required this.items, required this.activeKey, required this.onSelect});

  final List<_NavItem> items;
  final String activeKey;
  final ValueChanged<String> onSelect;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 260,
      decoration: BoxDecoration(color: AppColors.panel, borderRadius: BorderRadius.circular(20)),
      padding: const EdgeInsets.symmetric(vertical: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
            padding: const EdgeInsets.symmetric(vertical: 14),
            decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(14)),
            child: const Text(
              'UPRAVLJANJE',
              textAlign: TextAlign.center,
              style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, letterSpacing: 0.6),
            ),
          ),
          const SizedBox(height: 6),
          ...items.map((item) {
            final active = item.key == activeKey;
            return Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
              child: InkWell(
                borderRadius: BorderRadius.circular(10),
                onTap: () => onSelect(item.key),
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 10),
                  child: Text(
                    item.label,
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: active ? Colors.white : AppColors.accent,
                      fontWeight: FontWeight.bold,
                      fontSize: 13,
                    ),
                  ),
                ),
              ),
            );
          }),
        ],
      ),
    );
  }
}
