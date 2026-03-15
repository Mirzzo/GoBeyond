import 'package:dio/dio.dart';

import '../network/api_client.dart';

class PanelApiService {
  PanelApiService(this._client);

  final ApiClient _client;

  Future<Map<String, dynamic>> getOverviewReport(String accessToken) async {
    final response = await _client.dio.get<Map<String, dynamic>>(
      '/api/admin/reports/overview',
      options: _authOptions(accessToken),
    );
    return _asMap(response.data);
  }

  Future<Map<String, dynamic>> getMentorReport(
    String accessToken,
    int mentorId,
  ) async {
    final response = await _client.dio.get<Map<String, dynamic>>(
      '/api/admin/reports/mentors/$mentorId',
      options: _authOptions(accessToken),
    );
    return _asMap(response.data);
  }

  Future<List<Map<String, dynamic>>> getMentorRequests(
    String accessToken, {
    String? search,
  }) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/admin/mentor-requests',
      queryParameters: _searchParams(search),
      options: _authOptions(accessToken),
    );
    return _asList(response.data);
  }

  Future<Map<String, dynamic>> approveMentorRequest(
    String accessToken,
    int id,
  ) async {
    final response = await _client.dio.put<Map<String, dynamic>>(
      '/api/admin/mentor-requests/$id/approve',
      options: _authOptions(accessToken),
    );
    return _asMap(response.data);
  }

  Future<Map<String, dynamic>> rejectMentorRequest(
    String accessToken,
    int id,
  ) async {
    final response = await _client.dio.put<Map<String, dynamic>>(
      '/api/admin/mentor-requests/$id/reject',
      options: _authOptions(accessToken),
    );
    return _asMap(response.data);
  }

  Future<List<Map<String, dynamic>>> getMentors(
    String accessToken, {
    String? search,
  }) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/admin/mentors',
      queryParameters: _searchParams(search),
      options: _authOptions(accessToken),
    );
    return _asList(response.data);
  }

  Future<List<Map<String, dynamic>>> getClients(
    String accessToken, {
    String? search,
  }) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/admin/clients',
      queryParameters: _searchParams(search),
      options: _authOptions(accessToken),
    );
    return _asList(response.data);
  }

  Future<List<Map<String, dynamic>>> getSubscriptions(
    String accessToken, {
    String? search,
  }) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/admin/subscriptions',
      queryParameters: _searchParams(search),
      options: _authOptions(accessToken),
    );
    return _asList(response.data);
  }

  Future<Map<String, dynamic>> blockUser(
    String accessToken,
    int userId,
  ) async {
    final response = await _client.dio.put<Map<String, dynamic>>(
      '/api/admin/users/$userId/block',
      options: _authOptions(accessToken),
    );
    return _asMap(response.data);
  }

  Future<void> deleteUser(
    String accessToken,
    int userId,
  ) async {
    await _client.dio.delete<void>(
      '/api/admin/users/$userId',
      options: _authOptions(accessToken),
    );
  }

  Future<List<Map<String, dynamic>>> getCollaborationRequests(
    String accessToken, {
    String? search,
  }) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/mentors/collaboration-requests',
      queryParameters: _searchParams(search),
      options: _authOptions(accessToken),
    );
    return _asList(response.data);
  }

  Future<List<Map<String, dynamic>>> getSubscribers(
    String accessToken, {
    String? search,
  }) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/mentors/subscribers',
      queryParameters: _searchParams(search),
      options: _authOptions(accessToken),
    );
    return _asList(response.data);
  }

  Future<Map<String, dynamic>> getClientDetail(
    String accessToken,
    int clientUserId,
  ) async {
    final response = await _client.dio.get<Map<String, dynamic>>(
      '/api/mentors/clients/$clientUserId',
      options: _authOptions(accessToken),
    );
    return _asMap(response.data);
  }

  Future<List<Map<String, dynamic>>> getPlans(
    String accessToken, {
    String? search,
    String? status,
  }) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/training-plans',
      queryParameters: {
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
        if (status != null && status.trim().isNotEmpty) 'status': status.trim(),
      },
      options: _authOptions(accessToken),
    );
    return _asList(response.data);
  }

  Future<Map<String, dynamic>> getPlanDetail(
    String accessToken,
    int planId,
  ) async {
    final response = await _client.dio.get<Map<String, dynamic>>(
      '/api/training-plans/$planId',
      options: _authOptions(accessToken),
    );
    return _asMap(response.data);
  }

  Future<Map<String, dynamic>> createPlan(
    String accessToken,
    Map<String, dynamic> payload,
  ) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/training-plans',
      data: payload,
      options: _authOptions(accessToken),
    );
    return _asMap(response.data);
  }

  Future<Map<String, dynamic>> updatePlan(
    String accessToken,
    int planId,
    Map<String, dynamic> payload,
  ) async {
    final response = await _client.dio.put<Map<String, dynamic>>(
      '/api/training-plans/$planId',
      data: payload,
      options: _authOptions(accessToken),
    );
    return _asMap(response.data);
  }

  Future<Map<String, dynamic>> publishPlan(
    String accessToken,
    int planId,
  ) async {
    final response = await _client.dio.put<Map<String, dynamic>>(
      '/api/training-plans/$planId/publish',
      options: _authOptions(accessToken),
    );
    return _asMap(response.data);
  }

  Options _authOptions(String token) {
    return Options(headers: {'Authorization': 'Bearer $token'});
  }

  Map<String, dynamic> _searchParams(String? search) {
    return {
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
    };
  }

  Map<String, dynamic> _asMap(Map<String, dynamic>? value) {
    if (value == null) {
      throw Exception('Empty response from server.');
    }

    return value;
  }

  List<Map<String, dynamic>> _asList(List<dynamic>? value) {
    if (value == null) {
      return const [];
    }

    return value
        .whereType<Map<String, dynamic>>()
        .map((item) => Map<String, dynamic>.from(item))
        .toList();
  }
}
