import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../../core/network/dio_client.dart';
import '../models/user_profile.dart';

abstract class ProfileRepository {
  Future<UserProfile> getMyProfile();
  Future<UserProfile> updateMyProfile(Map<String, dynamic> payload);
  Future<String> uploadPhoto(Uint8List bytes, String fileName);
  Future<void> deletePhoto();
}

class ApiProfileRepository implements ProfileRepository {
  ApiProfileRepository({DioClient? client}) : _client = client ?? DioClient();

  final DioClient _client;

  @override
  Future<UserProfile> getMyProfile() async {
    final response =
        await _client.dio.get<Map<String, dynamic>>('/api/user-profile/me');
    return UserProfile.fromJson(response.data ?? const {});
  }

  @override
  Future<UserProfile> updateMyProfile(Map<String, dynamic> payload) async {
    final response = await _client.dio.put<Map<String, dynamic>>(
      '/api/user-profile/me',
      data: payload,
    );
    return UserProfile.fromJson(response.data ?? const {});
  }

  @override
  Future<String> uploadPhoto(Uint8List bytes, String fileName) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/user-profile/me/photo',
      data: FormData.fromMap({
        'file': MultipartFile.fromBytes(bytes, filename: fileName),
      }),
    );
    return response.data?['profileImageUrl']?.toString() ?? '';
  }

  @override
  Future<void> deletePhoto() async {
    await _client.dio.delete<void>('/api/user-profile/me/photo');
  }
}
