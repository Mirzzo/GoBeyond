import 'package:flutter/foundation.dart';

class AppConstants {
  static const _configuredUrl = String.fromEnvironment('GO_BEYOND_API_URL');
  static String get baseUrl => _configuredUrl.isNotEmpty
      ? _configuredUrl
      : !kIsWeb && defaultTargetPlatform == TargetPlatform.android
          ? 'http://10.0.2.2:5000'
          : 'http://localhost:5000';
  static const stripePublishableKey =
      String.fromEnvironment('GO_BEYOND_STRIPE_PUBLISHABLE_KEY');
  static const defaultPlanPrice = 19.99;
}
