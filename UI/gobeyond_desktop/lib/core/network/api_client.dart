import 'package:dio/dio.dart';

class ApiClient {
  static const _baseUrl = String.fromEnvironment(
    'GO_BEYOND_API_URL',
    defaultValue: 'http://localhost:5000',
  );

  ApiClient()
      : dio = Dio(
          BaseOptions(
            baseUrl: _baseUrl,
            connectTimeout: const Duration(seconds: 30),
            receiveTimeout: const Duration(seconds: 30),
            headers: {'Content-Type': 'application/json'},
          ),
        );

  final Dio dio;
}
