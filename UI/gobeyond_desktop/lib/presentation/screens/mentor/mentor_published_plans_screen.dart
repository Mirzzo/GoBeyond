import 'package:flutter/material.dart';

import '../../widgets/panel_card.dart';

class MentorPublishedPlansScreen extends StatelessWidget {
  const MentorPublishedPlansScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const PanelCard(
      title: 'Published Plans',
      description:
          'Search by client name and open plan preview/edit workflow for existing plans.',
    );
  }
}
