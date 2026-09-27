import 'package:dio/dio.dart';

import '../network/api_client.dart';

/// api-contract.md section 6 (Mentor, MentorOnly) — collaboration requests,
/// subscribers, and training-plan authoring.
class MentorService {
  Dio get _dio => ApiClient.instance.dio;

  List<Map<String, dynamic>> _list(Response<dynamic> response) {
    return (response.data as List<dynamic>? ?? const [])
        .map((e) => Map<String, dynamic>.from(e as Map))
        .toList();
  }

  Map<String, dynamic> _map(Response<dynamic> response) => Map<String, dynamic>.from(response.data as Map);

  Future<List<Map<String, dynamic>>> getCollaborationRequests({String? search}) async {
    final response = await _dio.get<List<dynamic>>('/api/mentors/me/collaboration-requests', queryParameters: {
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
    });
    return _list(response);
  }

  Future<Map<String, dynamic>> getCollaborationRequestDetail(int subscriptionId) async {
    final response = await _dio.get<Map<String, dynamic>>('/api/mentors/me/collaboration-requests/$subscriptionId');
    return _map(response);
  }

  Future<Map<String, dynamic>> acceptCollaborationRequest(int subscriptionId) async {
    final response = await _dio.put<Map<String, dynamic>>('/api/mentors/me/collaboration-requests/$subscriptionId/accept');
    return _map(response);
  }

  Future<String> rejectCollaborationRequest(int subscriptionId, String reason) async {
    final response = await _dio.put<Map<String, dynamic>>(
      '/api/mentors/me/collaboration-requests/$subscriptionId/reject',
      data: {'reason': reason},
    );
    return response.data?['message'] as String? ?? 'Zahtjev je odbijen.';
  }

  Future<List<Map<String, dynamic>>> getSubscribers({String? search, String? status}) async {
    final response = await _dio.get<List<dynamic>>('/api/mentors/me/subscribers', queryParameters: {
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
      if (status != null && status.isNotEmpty) 'status': status,
    });
    return _list(response);
  }

  Future<Map<String, dynamic>> getSubscriberDetail(int subscriptionId) async {
    final response = await _dio.get<Map<String, dynamic>>('/api/mentors/me/subscribers/$subscriptionId');
    return _map(response);
  }

  // ---- Training plans ----
  Future<List<Map<String, dynamic>>> getPlans({String? search, String? status}) async {
    final response = await _dio.get<List<dynamic>>('/api/training-plans', queryParameters: {
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
      if (status != null && status.isNotEmpty) 'status': status,
    });
    return _list(response);
  }

  Future<Map<String, dynamic>?> getPlanBySubscription(int subscriptionId) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>('/api/training-plans/by-subscription/$subscriptionId');
      return _map(response);
    } on DioException catch (e) {
      if (e.response?.statusCode == 404) return null;
      rethrow;
    }
  }

  Future<Map<String, dynamic>> getPlan(int id) async {
    final response = await _dio.get<Map<String, dynamic>>('/api/training-plans/$id');
    return _map(response);
  }

  Future<Map<String, dynamic>> createPlan(int subscriptionId, {String? motivationalQuote}) async {
    final response = await _dio.post<Map<String, dynamic>>('/api/training-plans', data: {
      'subscriptionId': subscriptionId,
      if (motivationalQuote != null && motivationalQuote.trim().isNotEmpty) 'motivationalQuote': motivationalQuote.trim(),
    });
    return _map(response);
  }

  Future<Map<String, dynamic>> updatePlan(int id, {String? motivationalQuote}) async {
    final response = await _dio.put<Map<String, dynamic>>('/api/training-plans/$id', data: {
      'motivationalQuote': motivationalQuote,
    });
    return _map(response);
  }

  Future<Map<String, dynamic>> saveDay(
    int planId,
    int dayOfWeek, {
    required int trainingDurationMinutes,
    required String trainingDescription,
    int? nutritionDurationMinutes,
    required String nutritionDescription,
  }) async {
    final response = await _dio.put<Map<String, dynamic>>(
      '/api/training-plans/$planId/days/$dayOfWeek',
      data: {
        'trainingDurationMinutes': trainingDurationMinutes,
        'trainingDescription': trainingDescription,
        'nutritionDurationMinutes': nutritionDurationMinutes,
        'nutritionDescription': nutritionDescription,
      },
    );
    return _map(response);
  }

  /// api-contract.md section 6 documents this as returning `PlanDetail`, but
  /// the Changelog v1.1 note that "all deletions" return `204` leaves this
  /// endpoint ambiguous — handle both: use the body if present, otherwise
  /// re-fetch the plan so callers always get a fresh `PlanDetail`.
  Future<Map<String, dynamic>> deleteDay(int planId, int dayOfWeek) async {
    final response = await _dio.delete<dynamic>('/api/training-plans/$planId/days/$dayOfWeek');
    if (response.data is Map) {
      return Map<String, dynamic>.from(response.data as Map);
    }
    return getPlan(planId);
  }

  Future<Map<String, dynamic>> publishPlan(int id) async {
    final response = await _dio.put<Map<String, dynamic>>('/api/training-plans/$id/publish');
    return _map(response);
  }

  Future<Map<String, dynamic>> archivePlan(int id) async {
    final response = await _dio.put<Map<String, dynamic>>('/api/training-plans/$id/archive');
    return _map(response);
  }
}
