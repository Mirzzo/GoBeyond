import '../models/auth_response.dart';
import '../network/api_client.dart';
import 'package:dio/dio.dart';

class AuthApiService {
  AuthApiService(this._client);

  final ApiClient _client;

  Future<List<Map<String, dynamic>>> trainingTypes() async {
    final response = await _client.dio.get<List<dynamic>>('/api/training-types');
    return (response.data ?? []).whereType<Map<String, dynamic>>().toList();
  }

  Future<Map<String, dynamic>> uploadMentorCertificate(String filePath, String fileName) async {
    final response = await _client.dio.post<Map<String, dynamic>>('/api/files/mentor-certificate',
      data: FormData.fromMap({'file': await MultipartFile.fromFile(filePath, filename: fileName)}),
      options: Options(contentType: 'multipart/form-data'));
    return response.data ?? <String, dynamic>{};
  }

  Future<void> registerMentor(Map<String, dynamic> payload) async {
    await _client.dio.post<Map<String, dynamic>>('/api/auth/register/mentor', data: payload);
  }

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
