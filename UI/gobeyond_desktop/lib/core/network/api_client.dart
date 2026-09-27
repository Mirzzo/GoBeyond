import 'dart:async';

import 'package:dio/dio.dart';

import '../config/app_config.dart';

/// Thin singleton wrapper around [Dio] shared by every service.
///
/// Holds the current access token and transparently refreshes it on a 401
/// response (via [onUnauthorized], wired up by `SessionController`) before
/// retrying the original request exactly once.
class ApiClient {
  ApiClient._internal() {
    dio = Dio(
      BaseOptions(
        baseUrl: AppConfig.apiBaseUrl,
        connectTimeout: const Duration(seconds: 20),
        receiveTimeout: const Duration(seconds: 20),
        headers: {'Accept': 'application/json'},
      ),
    );

    dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) {
          if (_accessToken != null && options.headers['Authorization'] == null) {
            options.headers['Authorization'] = 'Bearer $_accessToken';
          }
          handler.next(options);
        },
        onError: (error, handler) async {
          final isAuthRoute = error.requestOptions.path.contains('/api/auth/');
          if (error.response?.statusCode == 401 &&
              !isAuthRoute &&
              onUnauthorized != null &&
              error.requestOptions.extra['retried'] != true) {
            final refreshed = await _refreshOnce();
            if (refreshed) {
              try {
                final retryOptions = error.requestOptions;
                retryOptions.extra['retried'] = true;
                retryOptions.headers['Authorization'] = 'Bearer $_accessToken';
                final response = await dio.fetch(retryOptions);
                handler.resolve(response);
                return;
              } catch (retryError) {
                handler.next(error);
                return;
              }
            }
          }
          handler.next(error);
        },
      ),
    );
  }

  static final ApiClient instance = ApiClient._internal();

  late final Dio dio;

  String? _accessToken;

  /// Set by [SessionController]; must return true if the refresh succeeded.
  Future<bool> Function()? onUnauthorized;

  Completer<bool>? _refreshCompleter;

  void setAccessToken(String? token) {
    _accessToken = token;
  }

  Future<bool> _refreshOnce() async {
    if (_refreshCompleter != null) {
      return _refreshCompleter!.future;
    }
    final completer = Completer<bool>();
    _refreshCompleter = completer;
    try {
      final result = await onUnauthorized!.call();
      completer.complete(result);
    } catch (_) {
      completer.complete(false);
    } finally {
      _refreshCompleter = null;
    }
    return completer.future;
  }
}
