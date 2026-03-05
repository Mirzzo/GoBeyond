import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/session/session_controller.dart';

class DesktopHomeScreen extends StatelessWidget {
  const DesktopHomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final session = context.watch<SessionController>();

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
      body: Center(
        child: Text(
          'Logged in as ${session.user?.name} (${session.user?.role.name})',
          style: const TextStyle(fontSize: 18),
        ),
      ),
    );
  }
}
