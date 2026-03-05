import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/models/app_role.dart';
import '../../core/session/session_controller.dart';
import 'admin/admin_clients_screen.dart';
import 'admin/admin_dashboard_screen.dart';
import 'admin/admin_mentor_requests_screen.dart';
import 'admin/admin_mentors_screen.dart';
import 'admin/admin_subscriptions_screen.dart';
import 'mentor/mentor_collaboration_requests_screen.dart';
import 'mentor/mentor_create_plan_screen.dart';
import 'mentor/mentor_published_plans_screen.dart';
import 'mentor/mentor_subscribers_screen.dart';

class _PanelDestination {
  const _PanelDestination({
    required this.label,
    required this.icon,
    required this.page,
  });

  final String label;
  final IconData icon;
  final Widget page;
}

class DesktopHomeScreen extends StatefulWidget {
  const DesktopHomeScreen({super.key});

  @override
  State<DesktopHomeScreen> createState() => _DesktopHomeScreenState();
}

class _DesktopHomeScreenState extends State<DesktopHomeScreen> {
  int _selectedIndex = 0;

  static const _adminDestinations = <_PanelDestination>[
    _PanelDestination(
      label: 'Mentor Requests',
      icon: Icons.fact_check,
      page: AdminMentorRequestsScreen(),
    ),
    _PanelDestination(
      label: 'Mentors',
      icon: Icons.sports_gymnastics,
      page: AdminMentorsScreen(),
    ),
    _PanelDestination(
      label: 'Manage Subscriptions',
      icon: Icons.receipt_long,
      page: AdminSubscriptionsScreen(),
    ),
    _PanelDestination(
      label: 'Clients',
      icon: Icons.groups,
      page: AdminClientsScreen(),
    ),
    _PanelDestination(
      label: 'Dashboard',
      icon: Icons.dashboard,
      page: AdminDashboardScreen(),
    ),
  ];

  static const _mentorDestinations = <_PanelDestination>[
    _PanelDestination(
      label: 'Collaboration Requests',
      icon: Icons.handshake,
      page: MentorCollaborationRequestsScreen(),
    ),
    _PanelDestination(
      label: 'Published Plans',
      icon: Icons.task_alt,
      page: MentorPublishedPlansScreen(),
    ),
    _PanelDestination(
      label: 'Create Plan',
      icon: Icons.calendar_view_week,
      page: MentorCreatePlanScreen(),
    ),
    _PanelDestination(
      label: 'Subscribers',
      icon: Icons.people_alt,
      page: MentorSubscribersScreen(),
    ),
  ];

  @override
  Widget build(BuildContext context) {
    final session = context.watch<SessionController>();
    final role = session.user?.role;
    if (role == null) {
      return const SizedBox.shrink();
    }

    if (role == AppRole.client) {
      return Scaffold(
        appBar: AppBar(
          title: const Text('GoBeyond Desktop'),
          actions: [
            TextButton(
              onPressed: () => context.read<SessionController>().logout(),
              child: const Text('Logout'),
            ),
          ],
        ),
        body: const Center(
          child: Text('Client role uses mobile app. Desktop panel supports Admin and Mentor roles.'),
        ),
      );
    }

    final destinations = role == AppRole.admin ? _adminDestinations : _mentorDestinations;
    if (_selectedIndex >= destinations.length) {
      _selectedIndex = 0;
    }
    final page = destinations[_selectedIndex].page;

    return Scaffold(
      body: Row(
        children: [
          NavigationRail(
            backgroundColor: const Color(0xFF2D2D2D),
            minWidth: 96,
            selectedIndex: _selectedIndex,
            groupAlignment: -0.9,
            onDestinationSelected: (index) => setState(() => _selectedIndex = index),
            labelType: NavigationRailLabelType.all,
            leading: const Padding(
              padding: EdgeInsets.only(top: 16, bottom: 12),
              child: Text(
                'MANAGEMENT',
                style: TextStyle(
                  color: Color(0xFFFFD700),
                  fontWeight: FontWeight.w600,
                  letterSpacing: 0.6,
                ),
              ),
            ),
            destinations: [
              for (final item in destinations)
                NavigationRailDestination(
                  icon: Icon(item.icon),
                  label: Text(item.label, textAlign: TextAlign.center),
                ),
            ],
          ),
          const VerticalDivider(width: 1, color: Color(0x40FFD700)),
          Expanded(
            child: Column(
              children: [
                Container(
                  height: 78,
                  margin: const EdgeInsets.all(14),
                  padding: const EdgeInsets.symmetric(horizontal: 18),
                  decoration: BoxDecoration(
                    color: const Color(0xFF2D2D2D),
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(color: const Color(0x40FFD700)),
                  ),
                  child: Row(
                    children: [
                      const Text(
                        'Home   |   Dashboard',
                        style: TextStyle(color: Color(0xFFBDBDBD)),
                      ),
                      const Spacer(),
                      Text(
                        '${session.user?.role.name.toUpperCase()} | ${session.user?.name}',
                      ),
                      const SizedBox(width: 12),
                      TextButton(
                        onPressed: () => context.read<SessionController>().logout(),
                        child: const Text('Logout'),
                      ),
                    ],
                  ),
                ),
                Expanded(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(14, 0, 14, 14),
                    child: page,
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
