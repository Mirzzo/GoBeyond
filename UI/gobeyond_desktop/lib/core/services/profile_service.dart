import 'package:dio/dio.dart';

import '../network/api_client.dart';

/// api-contract.md sections 4 (Profil) and the mentor `/me/certificates` part of section 6.
class ProfileService {
  Dio get _dio => ApiClient.instance.dio;

  Future<Map<String, dynamic>> getMe() async {
    final response = await _dio.get<Map<String, dynamic>>('/api/user-profile/me');
    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<Map<String, dynamic>> updateMe(Map<String, dynamic> payload) async {
    final response = await _dio.put<Map<String, dynamic>>('/api/user-profile/me', data: payload);
    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<String?> uploadPhoto(String filePath) async {
    final response = await _dio.post<Map<String, dynamic>>(
      '/api/user-profile/me/photo',
      data: FormData.fromMap({'file': await MultipartFile.fromFile(filePath)}),
    );
    return response.data?['profileImageUrl'] as String?;
  }

  Future<void> deletePhoto() async {
    await _dio.delete<void>('/api/user-profile/me/photo');
  }

  Future<List<Map<String, dynamic>>> myCertificates() async {
    final response = await _dio.get<List<dynamic>>('/api/mentors/me/certificates');
    return (response.data ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  Future<List<Map<String, dynamic>>> addCertificates(List<String> filePaths) async {
    final files = await Future.wait(filePaths.map((p) => MultipartFile.fromFile(p)));
    final response = await _dio.post<List<dynamic>>(
      '/api/mentors/me/certificates',
      data: FormData()..files.addAll(files.map((f) => MapEntry('files', f))),
    );
    return (response.data ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  Future<void> deleteCertificate(int id) async {
    await _dio.delete<void>('/api/mentors/me/certificates/$id');
  }
}
