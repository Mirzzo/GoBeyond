import 'package:dio/dio.dart';

import '../models/auth_user.dart';
import '../network/api_client.dart';

/// api-contract.md section 2 (Auth).
class AuthService {
  Dio get _dio => ApiClient.instance.dio;

  Future<AuthResponse> login({required String username, required String password}) async {
    final response = await _dio.post<Map<String, dynamic>>(
      '/api/auth/login',
      data: {'username': username, 'password': password},
    );
    return AuthResponse.fromJson(response.data!);
  }

  Future<AuthResponse> refresh({required String refreshToken}) async {
    final response = await _dio.post<Map<String, dynamic>>(
      '/api/auth/refresh',
      data: {'refreshToken': refreshToken},
    );
    return AuthResponse.fromJson(response.data!);
  }

  Future<void> logout({required String refreshToken}) async {
    await _dio.post<void>('/api/auth/logout', data: {'refreshToken': refreshToken});
  }

  Future<String> changePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmPassword,
  }) async {
    final response = await _dio.post<Map<String, dynamic>>(
      '/api/auth/change-password',
      data: {
        'currentPassword': currentPassword,
        'newPassword': newPassword,
        'confirmPassword': confirmPassword,
      },
    );
    return response.data?['message'] as String? ?? 'Lozinka je uspješno promijenjena.';
  }

  /// multipart/form-data register/mentor (api-contract.md section 2).
  Future<String> registerMentor({
    required Map<String, String> fields,
    required List<int> specializationIds,
    required List<MultipartFile> certificates,
  }) async {
    final formMap = <String, dynamic>{...fields};
    formMap['specializationIds'] = specializationIds.map((e) => e.toString()).toList();
    formMap['certificates'] = certificates;

    final response = await _dio.post<Map<String, dynamic>>(
      '/api/auth/register/mentor',
      data: FormData.fromMap(formMap),
    );
    return response.data?['message'] as String? ?? 'Registracija je uspješna.';
  }
}
