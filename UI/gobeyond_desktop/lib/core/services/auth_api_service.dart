import '../models/auth_response.dart';
import '../network/api_client.dart';

class AuthApiService {
  AuthApiService(this._client);

  final ApiClient _client;

  Future<AuthResponse> login({required String email, required String password}) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/auth/login',
      data: {
        'email': email,
        'password': password,
      },
    );

    final payload = response.data;
    if (payload == null) {
      throw Exception('Empty login response from server.');
    }

    return AuthResponse.fromJson(payload);
  }

  Future<AuthResponse> refresh({required String refreshToken}) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/auth/refresh',
      data: {
        'refreshToken': refreshToken,
      },
    );

    final payload = response.data;
    if (payload == null) {
      throw Exception('Empty refresh response from server.');
    }

    return AuthResponse.fromJson(payload);
  }
}
