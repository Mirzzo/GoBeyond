import 'package:flutter/widgets.dart';

import 'auth_controller.dart';

class AuthScope extends InheritedNotifier<AuthController> {
  const AuthScope({
    super.key,
    required AuthController controller,
    required super.child,
  }) : super(notifier: controller);

  static AuthController of(BuildContext context) {
    final scope = context.dependOnInheritedWidgetOfExactType<AuthScope>();
    if (scope == null || scope.notifier == null) {
      throw FlutterError('AuthScope is missing from the widget tree.');
    }

    return scope.notifier!;
  }

  static AuthController read(BuildContext context) {
    final element = context.getElementForInheritedWidgetOfExactType<AuthScope>();
    final scope = element?.widget as AuthScope?;
    if (scope == null || scope.notifier == null) {
      throw FlutterError('AuthScope is missing from the widget tree.');
    }

    return scope.notifier!;
  }
}
