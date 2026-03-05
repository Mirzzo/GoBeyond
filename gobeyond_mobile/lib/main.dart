import 'package:flutter/material.dart';
import 'core/theme/app_theme.dart';
import 'presentation/screens/common/splash_screen.dart';

void main() {
  runApp(const GoBeyondApp());
}

class GoBeyondApp extends StatelessWidget {
  const GoBeyondApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'GoBeyond',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.dark,
      home: const SplashScreen(),
    );
  }
}
