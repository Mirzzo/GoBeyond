import 'package:flutter/material.dart';

import '../../widgets/panel_card.dart';

class MentorCollaborationRequestsScreen extends StatelessWidget {
  const MentorCollaborationRequestsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const PanelCard(
      title: 'Collaboration Requests',
      description:
          'Pending client requests with questionnaire preview and Create Plan action.',
    );
  }
}
