import '../../core/network/dio_client.dart';

class AuthRepository {
  AuthRepository(this._client);

  final DioClient _client;

  Future<Map<String, dynamic>> login(String email, String password) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/auth/login',
      data: {'email': email, 'password': password},
    );
    return _asMap(response.data);
  }

  Future<Map<String, dynamic>> registerClient(Map<String, dynamic> payload) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/auth/register/client',
      data: payload,
    );
    return _asMap(response.data);
  }

  Future<Map<String, dynamic>> getMyProfile() async {
    final response = await _client.dio.get<Map<String, dynamic>>('/api/user-profile/me');
    return _asMap(response.data);
  }

  Future<Map<String, dynamic>> updateMyProfile(Map<String, dynamic> payload) async {
    final response = await _client.dio.put<Map<String, dynamic>>(
      '/api/user-profile/me',
      data: payload,
    );
    return _asMap(response.data);
  }

  Map<String, dynamic> _asMap(Map<String, dynamic>? value) {
    if (value == null) {
      throw Exception('Empty response from server.');
    }

    return value;
  }
}
