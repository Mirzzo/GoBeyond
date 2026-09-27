import '../../core/network/dio_client.dart';

/// Abstract so screens can be unit-tested against a fake without touching
/// the network (see test/registration_validation_test.dart).
abstract class AuthRepository {
  Future<Map<String, dynamic>> login(String usernameOrEmail, String password);

  Future<Map<String, dynamic>> registerClient(Map<String, dynamic> payload);

  Future<void> logout(String refreshToken);

  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmPassword,
  });
}

class ApiAuthRepository implements AuthRepository {
  ApiAuthRepository({DioClient? client}) : _client = client ?? DioClient();

  final DioClient _client;

  @override
  Future<Map<String, dynamic>> login(
      String usernameOrEmail, String password) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/auth/login',
      data: {'username': usernameOrEmail, 'password': password},
    );
    return response.data ?? const {};
  }

  @override
  Future<Map<String, dynamic>> registerClient(
      Map<String, dynamic> payload) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/auth/register/client',
      data: payload,
    );
    return response.data ?? const {};
  }

  @override
  Future<void> logout(String refreshToken) async {
    await _client.dio.post<void>(
      '/api/auth/logout',
      data: {'refreshToken': refreshToken},
    );
  }

  @override
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmPassword,
  }) async {
    await _client.dio.post<void>('/api/auth/change-password', data: {
      'currentPassword': currentPassword,
      'newPassword': newPassword,
      'confirmPassword': confirmPassword,
    });
  }
}
