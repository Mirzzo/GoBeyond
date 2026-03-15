import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:gobeyond_mobile/core/auth/auth_controller.dart';
import 'package:gobeyond_mobile/core/auth/auth_scope.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/main.dart';
import 'package:gobeyond_mobile/presentation/screens/auth/login_register_screen.dart';

void main() {
  testWidgets('renders auth entry screen', (WidgetTester tester) async {
    final controller = _TestAuthController();

    await tester.pumpWidget(
      AuthScope(
        controller: controller,
        child: MaterialApp(
          theme: AppTheme.theme,
          home: const LoginRegisterScreen(),
        ),
      ),
    );

    expect(find.text('Login / Register'), findsOneWidget);
    expect(find.text('Welcome back'), findsOneWidget);
  });

  testWidgets('app bootstrap renders a material app', (WidgetTester tester) async {
    await tester.pumpWidget(const GoBeyondApp());
    await tester.pump();

    expect(find.byType(MaterialApp), findsOneWidget);
  });
}

class _TestAuthController extends AuthController {
  @override
  bool get isBusy => false;

  @override
  String? get errorMessage => null;

  @override
  Future<bool> login({required String email, required String password}) async => true;

  @override
  Future<bool> registerClient(Map<String, dynamic> payload) async => true;
}
