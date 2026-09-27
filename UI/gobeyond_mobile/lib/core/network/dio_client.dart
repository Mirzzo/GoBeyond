import 'package:dio/dio.dart';

import '../auth/session_storage.dart';
import '../constants/app_constants.dart';

/// Endpoints that must never trigger a refresh-and-retry on 401: they are
/// either anonymous (a 401 there means bad credentials, not an expired
/// session) or the refresh call itself (retrying it would recurse). Every
/// other authenticated endpoint — including `/api/auth/change-password`,
/// which needs the current access token like any other authenticated call —
/// is retried after a refresh.
const _noRefreshRetryPaths = {
  '/api/auth/login',
  '/api/auth/register/client',
  '/api/auth/register/mentor',
  '/api/auth/refresh',
  '/api/auth/logout',
};

/// App-wide singleton Dio wrapper: attaches the bearer access token to every
/// request, and transparently refreshes it via `POST /api/auth/refresh` on a
/// single 401 before retrying the original request once. If the refresh
/// itself fails, [onSessionExpired] is invoked so the app can log the user
/// out and return to the login screen.
class DioClient {
  factory DioClient() => instance;

  DioClient._internal()
      : dio = Dio(BaseOptions(
          baseUrl: AppConstants.apiBaseUrl,
          connectTimeout: const Duration(seconds: 20),
          receiveTimeout: const Duration(seconds: 20),
        )) {
    _refreshDio = Dio(BaseOptions(baseUrl: AppConstants.apiBaseUrl));

    dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) async {
          final token = await _storage.accessToken;
          if (token != null && token.isNotEmpty) {
            options.headers['Authorization'] = 'Bearer $token';
          }
          handler.next(options);
        },
        onError: (error, handler) async {
          final path = error.requestOptions.path;
          if (error.response?.statusCode == 401 &&
              !_noRefreshRetryPaths.contains(path) &&
              error.requestOptions.extra['retried'] != true) {
            try {
              final retried =
                  await _retryWithRefreshedToken(error.requestOptions);
              handler.resolve(retried);
              return;
            } catch (_) {
              await _storage.clear();
              onSessionExpired?.call();
            }
          }
          handler.next(error);
        },
      ),
    );
  }

  static final DioClient instance = DioClient._internal();

  final Dio dio;
  late final Dio _refreshDio;
  final SessionStorage _storage = SessionStorage();

  /// Set by the auth layer; called when the refresh token is invalid/expired
  /// so the app can force a logout and return to the login screen.
  void Function()? onSessionExpired;

  Future<Response<dynamic>> _retryWithRefreshedToken(
      RequestOptions requestOptions) async {
    // Single-flight guard: refresh tokens rotate server-side (the old one
    // stops working the instant `/api/auth/refresh` succeeds), so if two
    // requests 401 at once, the second one must await the SAME in-flight
    // refresh call rather than firing its own with the now-stale token —
    // that second call would otherwise fail and wrongly log the user out.
    // `_refreshFuture ??= ...` plus the lack of any `await` before this
    // line's own `await` means this check-and-set can't race with another
    // call to this method (Dart only yields at `await` points).
    _refreshFuture ??= _refreshAccessToken();
    final newToken = await _refreshFuture;
    _refreshFuture = null;

    if (newToken == null) {
      throw StateError('Token refresh failed.');
    }

    requestOptions.headers['Authorization'] = 'Bearer $newToken';
    requestOptions.extra['retried'] = true;
    return dio.fetch<dynamic>(requestOptions);
  }

  Future<String?>? _refreshFuture;

  Future<String?> _refreshAccessToken() async {
    final refreshToken = await _storage.refreshToken;
    if (refreshToken == null || refreshToken.isEmpty) return null;

    try {
      final response = await _refreshDio.post<Map<String, dynamic>>(
        '/api/auth/refresh',
        data: {'refreshToken': refreshToken},
      );
      final data = response.data;
      final accessToken = data?['accessToken']?.toString();
      final newRefreshToken = data?['refreshToken']?.toString();
      if (accessToken == null || newRefreshToken == null) return null;

      await _storage.saveTokens(
        accessToken: accessToken,
        refreshToken: newRefreshToken,
      );
      return accessToken;
    } catch (_) {
      return null;
    }
  }
}
