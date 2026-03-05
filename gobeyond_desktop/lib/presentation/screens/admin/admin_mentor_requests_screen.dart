import 'package:flutter/material.dart';

import 'panel_card.dart';

class AdminMentorRequestsScreen extends StatelessWidget {
  const AdminMentorRequestsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const PanelCard(
      title: 'Mentor Requests',
      description:
          'Pending mentor approvals with certificate preview and approve/reject actions.',
    );
  }
}
