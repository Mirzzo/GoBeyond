import 'package:dio/dio.dart';

import '../models/user_profile.dart';
import '../network/api_client.dart';

class UserProfileApiService {
  UserProfileApiService(this._client);

  final ApiClient _client;

  Future<UserProfile> getProfile({required String accessToken}) async {
    final response = await _client.dio.get<Map<String, dynamic>>(
      '/api/user-profile/me',
      options: _authOptions(accessToken),
    );
    return _parseProfile(response.data);
  }

  Future<UserProfile> createProfile({
    required String accessToken,
    required UserProfileUpsertRequest request,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/user-profile/me',
      data: request.toJson(),
      options: _authOptions(accessToken),
    );
    return _parseProfile(response.data);
  }

  Future<UserProfile> updateProfile({
    required String accessToken,
    required UserProfileUpsertRequest request,
  }) async {
    final response = await _client.dio.put<Map<String, dynamic>>(
      '/api/user-profile/me',
      data: request.toJson(),
      options: _authOptions(accessToken),
    );
    return _parseProfile(response.data);
  }

  Future<void> deleteProfile({required String accessToken}) async {
    await _client.dio.delete<void>(
      '/api/user-profile/me',
      options: _authOptions(accessToken),
    );
  }

  Options _authOptions(String token) {
    return Options(
      headers: {
        'Authorization': 'Bearer $token',
      },
    );
  }

  UserProfile _parseProfile(Map<String, dynamic>? payload) {
    if (payload == null) {
      throw Exception('Empty profile response from server.');
    }

    return UserProfile.fromJson(payload);
  }
}
