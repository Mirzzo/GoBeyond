/// Single, centralized place for environment/configuration values.
///
/// The API base URL is provided via `--dart-define=GO_BEYOND_API_URL=...`
/// (see docs/api-contract.md, section 0). Nothing else in this app should
/// read `String.fromEnvironment` or hardcode a host/port.
class AppConfig {
  const AppConfig._();

  static const String apiBaseUrl = String.fromEnvironment(
    'GO_BEYOND_API_URL',
    defaultValue: 'http://localhost:5000',
  );

  /// The API returns images/files as relative paths (e.g. `/uploads/...`).
  /// Every place that renders such a path must go through this helper so the
  /// base URL prefixing logic lives in exactly one place.
  static String? resolveUrl(String? path) {
    if (path == null || path.trim().isEmpty) {
      return null;
    }
    if (path.startsWith('http://') || path.startsWith('https://')) {
      return path;
    }
    final normalized = path.startsWith('/') ? path : '/$path';
    return '$apiBaseUrl$normalized';
  }
}
