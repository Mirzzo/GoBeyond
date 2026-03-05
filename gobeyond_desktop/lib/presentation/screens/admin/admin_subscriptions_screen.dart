import 'package:flutter/material.dart';

import 'panel_card.dart';

class AdminSubscriptionsScreen extends StatelessWidget {
  const AdminSubscriptionsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const PanelCard(
      title: 'Manage Subscriptions',
      description: 'Subscription list with status management and admin actions.',
    );
  }
}
