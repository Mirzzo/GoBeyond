import 'package:flutter/foundation.dart';

/// Single source of truth for the backend base URL. Configurable via
/// `flutter run --dart-define=GO_BEYOND_API_URL=...` per the course rules
/// (no hardcoded host/port anywhere else in the app).
class AppConstants {
  const AppConstants._();

  static const _configuredUrl = String.fromEnvironment('GO_BEYOND_API_URL');

  static String get apiBaseUrl => _configuredUrl.isNotEmpty
      ? _configuredUrl
      : (!kIsWeb && defaultTargetPlatform == TargetPlatform.android)
          ? 'http://10.0.2.2:5000'
          : 'http://localhost:5000';

  static const heartbeatInterval = Duration(seconds: 60);
}
