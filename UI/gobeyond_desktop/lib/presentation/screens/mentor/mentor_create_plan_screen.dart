import 'package:flutter/material.dart';

import '../../widgets/panel_card.dart';

class MentorCreatePlanScreen extends StatelessWidget {
  const MentorCreatePlanScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const PanelCard(
      title: 'Create Training Plan',
      description:
          'Seven-day grid with Training/Nutrition tab editing and publish action.',
    );
  }
}
