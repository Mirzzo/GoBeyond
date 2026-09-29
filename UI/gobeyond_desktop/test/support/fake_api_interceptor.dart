import 'package:dio/dio.dart';
import 'package:gobeyond_desktop/core/network/api_client.dart';

/// Fakes backend responses on [ApiClient.instance.dio] so a widget test can
/// drive the real screen/dialog widgets end to end (they all call the API
/// through this one shared Dio instance) instead of exercising a copy of
/// their logic in a throwaway host widget.
///
/// Register a responder per method+path with [on]; each responder gets the
/// live [RequestInterceptorHandler] and decides when to call
/// `handler.resolve(...)`/`handler.reject(...)` — immediately, for a normal
/// response, or later (after the test has moved the widget tree on, e.g.
/// closed a dialog), to reproduce a request that is still in flight when
/// something else happens.
class FakeApiInterceptor extends Interceptor {
  final _responders = <String, void Function(RequestOptions options, RequestInterceptorHandler handler)>{};

  void on(String method, String path, void Function(RequestOptions options, RequestInterceptorHandler handler) responder) {
    _responders['${method.toUpperCase()} $path'] = responder;
  }

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    final responder = _responders['${options.method.toUpperCase()} ${options.path}'];
    if (responder == null) {
      handler.reject(DioException(requestOptions: options, error: 'No fake responder for ${options.method} ${options.path}'));
      return;
    }
    responder(options, handler);
  }

  /// Installs on the shared client and returns a function that removes it —
  /// call it from `tearDown` so one test's fakes never leak into the next.
  static FakeApiInterceptor install() {
    final interceptor = FakeApiInterceptor();
    ApiClient.instance.dio.interceptors.add(interceptor);
    return interceptor;
  }

  void uninstall() => ApiClient.instance.dio.interceptors.remove(this);
}

Response<T> fakeResponse<T>(RequestOptions options, T data, {int statusCode = 200}) =>
    Response<T>(requestOptions: options, data: data, statusCode: statusCode);

DioException fakeError(RequestOptions options, {int statusCode = 400, Object? data}) => DioException(
      requestOptions: options,
      response: Response<Object?>(requestOptions: options, statusCode: statusCode, data: data),
      type: DioExceptionType.badResponse,
    );
