import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'core/auth/auth_controller.dart';
import 'core/auth/auth_scope.dart';
import 'core/navigation/app_navigator.dart';
import 'core/theme/app_theme.dart';
import 'presentation/screens/auth/login_register_screen.dart';
import 'presentation/screens/common/splash_screen.dart';
import 'presentation/screens/home/home_screen.dart';

void main() {
  runApp(const GoBeyondApp());
}

class GoBeyondApp extends StatefulWidget {
  const GoBeyondApp({super.key});

  @override
  State<GoBeyondApp> createState() => _GoBeyondAppState();
}

class _GoBeyondAppState extends State<GoBeyondApp> with WidgetsBindingObserver {
  late final AuthController _authController;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _authController = AuthController()..hydrate();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    _authController.handleLifecycleChange(state);
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
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
        navigatorKey: AppNavigator.key,
        theme: AppTheme.theme,
        locale: const Locale('bs'),
        supportedLocales: const [Locale('bs')],
        localizationsDelegates: const [
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: const _AppBootstrap(),
      ),
    );
  }
}

/// Decides the initial screen once the session has been hydrated from
/// secure storage: the Client-only Home screen when logged in, otherwise
/// login/registration.
class _AppBootstrap extends StatelessWidget {
  const _AppBootstrap();

  @override
  Widget build(BuildContext context) {
    final auth = AuthScope.of(context);

    if (!auth.isHydrated) {
      return const SplashScreen();
    }

    if (auth.isAuthenticated) {
      return const HomeScreen();
    }

    return const LoginRegisterScreen();
  }
}
