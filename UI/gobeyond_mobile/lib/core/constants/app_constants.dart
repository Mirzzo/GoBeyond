class AppConstants {
  static const baseUrl = String.fromEnvironment(
    'GO_BEYOND_API_URL',
    defaultValue: 'http://localhost:5000',
  );
  static const defaultPlanPrice = 19.99;
}
