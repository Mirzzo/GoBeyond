import 'package:flutter/widgets.dart';

/// Global navigator key so code with no [BuildContext] (the Dio 401/refresh
/// interceptor, via [AuthController]'s session-expired callback) can still
/// pop back to the login screen.
class AppNavigator {
  const AppNavigator._();

  static final GlobalKey<NavigatorState> key = GlobalKey<NavigatorState>();

  static NavigatorState? get state => key.currentState;
}
