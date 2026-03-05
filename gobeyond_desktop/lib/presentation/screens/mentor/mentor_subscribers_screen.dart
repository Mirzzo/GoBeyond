import 'package:flutter/material.dart';

import '../../widgets/panel_card.dart';

class MentorSubscribersScreen extends StatelessWidget {
  const MentorSubscribersScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const PanelCard(
      title: 'Subscribers',
      description: 'Active subscriber list with quick access to client detail view.',
    );
  }
}
