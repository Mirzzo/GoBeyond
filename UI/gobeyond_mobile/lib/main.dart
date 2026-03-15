import 'package:flutter/material.dart';

import 'core/auth/auth_controller.dart';
import 'core/auth/auth_scope.dart';
import 'core/theme/app_theme.dart';
import 'presentation/screens/auth/login_register_screen.dart';
import 'presentation/screens/common/client_shell_screen.dart';

void main() {
  runApp(const GoBeyondApp());
}

class GoBeyondApp extends StatefulWidget {
  const GoBeyondApp({super.key});

  @override
  State<GoBeyondApp> createState() => _GoBeyondAppState();
}

class _GoBeyondAppState extends State<GoBeyondApp> {
  late final AuthController _authController;

  @override
  void initState() {
    super.initState();
    _authController = AuthController()..hydrate();
  }

  @override
  void dispose() {
    _authController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AuthScope(
      controller: _authController,
      child: MaterialApp(
        title: 'GoBeyond',
        debugShowCheckedModeBanner: false,
        theme: AppTheme.theme,
        home: const _AppBootstrap(),
      ),
    );
  }
}

class _AppBootstrap extends StatelessWidget {
  const _AppBootstrap();

  @override
  Widget build(BuildContext context) {
    final auth = AuthScope.of(context);

    if (!auth.isHydrated) {
      return const Scaffold(
        body: Center(child: CircularProgressIndicator()),
      );
    }

    if (auth.isAuthenticated) {
      return const ClientShellScreen();
    }

    return const LoginRegisterScreen();
  }
}
