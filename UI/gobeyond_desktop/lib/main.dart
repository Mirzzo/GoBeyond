import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'core/session/session_controller.dart';
import 'core/theme/app_theme.dart';
import 'presentation/screens/home_shell.dart';
import 'presentation/screens/login_screen.dart';

void main() {
  runApp(const GoBeyondDesktopApp());
}

class GoBeyondDesktopApp extends StatelessWidget {
  const GoBeyondDesktopApp({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => SessionController(),
      child: MaterialApp(
        title: 'GoBeyond Desktop',
        debugShowCheckedModeBanner: false,
        theme: AppTheme.dark,
        home: const AppBootstrap(),
      ),
    );
  }
}

class AppBootstrap extends StatefulWidget {
  const AppBootstrap({super.key});

  @override
  State<AppBootstrap> createState() => _AppBootstrapState();
}

class _AppBootstrapState extends State<AppBootstrap> {
  @override
  void initState() {
    super.initState();
    context.read<SessionController>().hydrate();
  }

  @override
  Widget build(BuildContext context) {
    final session = context.watch<SessionController>();

    if (!session.isHydrated) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    return session.isAuthenticated ? const HomeShell() : const LoginScreen();
  }
}
