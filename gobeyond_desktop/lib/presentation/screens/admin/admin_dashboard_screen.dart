import 'package:flutter/material.dart';

import 'panel_card.dart';

class AdminDashboardScreen extends StatelessWidget {
  const AdminDashboardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const PanelCard(
      title: 'Dashboard',
      description:
          'Platform overview stats cards (subscribers, earnings, active plans, growth).',
    );
  }
}
