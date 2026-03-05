import 'package:flutter/material.dart';

import 'panel_card.dart';

class AdminMentorsScreen extends StatelessWidget {
  const AdminMentorsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const PanelCard(
      title: 'Active Mentors',
      description:
          'Search/filter mentors and review report, edit, and delete actions.',
    );
  }
}
