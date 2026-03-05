import 'package:flutter/material.dart';

import 'panel_card.dart';

class AdminClientsScreen extends StatelessWidget {
  const AdminClientsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const PanelCard(
      title: 'Clients',
      description: 'Client management table with block/delete and profile actions.',
    );
  }
}
