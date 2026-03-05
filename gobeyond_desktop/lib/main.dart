import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'core/session/session_controller.dart';
import 'core/theme/app_theme.dart';
import 'presentation/screens/desktop_home_screen.dart';
import 'presentation/screens/login_screen.dart';

void main() {
  runApp(
    ChangeNotifierProvider(
      create: (_) => SessionController(),
      child: const GoBeyondDesktopApp(),
    ),
  );
}

class GoBeyondDesktopApp extends StatelessWidget {
  const GoBeyondDesktopApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'GoBeyond Desktop',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.dark,
      home: const AppBootstrap(),
    );
  }
}

class AppBootstrap extends StatefulWidget {
  const AppBootstrap({super.key});

  @override
  State<AppBootstrap> createState() => _AppBootstrapState();
}

class _AppBootstrapState extends State<AppBootstrap> {
  late final SessionController _sessionController;

  @override
  void initState() {
    super.initState();
    _sessionController = context.read<SessionController>();
    _sessionController.hydrate();
  }

  @override
  Widget build(BuildContext context) {
    final session = context.watch<SessionController>();

    if (!session.isHydrated) {
      return const Scaffold(
        body: Center(child: CircularProgressIndicator()),
      );
    }

    if (session.isAuthenticated) {
      return const DesktopHomeScreen();
    }

    return const LoginScreen();
  }
}
