import 'package:dio/dio.dart';

import '../network/api_client.dart';

/// api-contract.md section 5 (Admin, AdminOnly).
class AdminService {
  Dio get _dio => ApiClient.instance.dio;

  List<Map<String, dynamic>> _list(Response<dynamic> response) {
    return (response.data as List<dynamic>? ?? const [])
        .map((e) => Map<String, dynamic>.from(e as Map))
        .toList();
  }

  Map<String, dynamic> _map(Response<dynamic> response) => Map<String, dynamic>.from(response.data as Map);

  // ---- Users ----
  Future<List<Map<String, dynamic>>> getUsers({String? search, String? role, bool? isActive}) async {
    final response = await _dio.get<List<dynamic>>('/api/admin/users', queryParameters: {
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
      'role': ?role,
      'isActive': ?isActive,
    });
    return _list(response);
  }

  Future<Map<String, dynamic>> getUserDetail(int id) async {
    final response = await _dio.get<Map<String, dynamic>>('/api/admin/users/$id');
    return _map(response);
  }

  Future<Map<String, dynamic>> updateUser(int id, Map<String, dynamic> payload) async {
    final response = await _dio.put<Map<String, dynamic>>('/api/admin/users/$id', data: payload);
    return _map(response);
  }

  Future<String> resetPassword(int id, {required String newPassword, required String confirmPassword}) async {
    final response = await _dio.put<Map<String, dynamic>>(
      '/api/admin/users/$id/reset-password',
      data: {'newPassword': newPassword, 'confirmPassword': confirmPassword},
    );
    return response.data?['message'] as String? ?? 'Lozinka je uspješno resetovana.';
  }

  Future<Map<String, dynamic>> blockUser(int id) async {
    final response = await _dio.put<Map<String, dynamic>>('/api/admin/users/$id/block');
    return _map(response);
  }

  Future<Map<String, dynamic>> unblockUser(int id) async {
    final response = await _dio.put<Map<String, dynamic>>('/api/admin/users/$id/unblock');
    return _map(response);
  }

  Future<void> deleteUser(int id) async {
    await _dio.delete<void>('/api/admin/users/$id');
  }

  // ---- Mentors & requests ----
  Future<List<Map<String, dynamic>>> getMentors({String? search, int? trainingTypeId, bool? isActive}) async {
    final response = await _dio.get<List<dynamic>>('/api/admin/mentors', queryParameters: {
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
      'trainingTypeId': ?trainingTypeId,
      'isActive': ?isActive,
    });
    return _list(response);
  }

  Future<List<Map<String, dynamic>>> getMentorRequests({String? search, int? trainingTypeId}) async {
    final response = await _dio.get<List<dynamic>>('/api/admin/mentor-requests', queryParameters: {
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
      'trainingTypeId': ?trainingTypeId,
    });
    return _list(response);
  }

  Future<Map<String, dynamic>> getMentorRequestDetail(int mentorProfileId) async {
    final response = await _dio.get<Map<String, dynamic>>('/api/admin/mentor-requests/$mentorProfileId');
    return _map(response);
  }

  Future<String> approveMentorRequest(int mentorProfileId) async {
    final response = await _dio.put<Map<String, dynamic>>('/api/admin/mentor-requests/$mentorProfileId/approve');
    return response.data?['message'] as String? ?? 'Mentor je odobren.';
  }

  Future<String> rejectMentorRequest(int mentorProfileId, String reason) async {
    final response = await _dio.put<Map<String, dynamic>>(
      '/api/admin/mentor-requests/$mentorProfileId/reject',
      data: {'reason': reason},
    );
    return response.data?['message'] as String? ?? 'Zahtjev je odbijen.';
  }

  Future<Map<String, dynamic>> verifyCertificate(int certificateId) async {
    final response = await _dio.put<Map<String, dynamic>>('/api/admin/certificates/$certificateId/verify');
    return _map(response);
  }

  Future<List<Map<String, dynamic>>> getMentorCertificates(int mentorProfileId) async {
    final response = await _dio.get<List<dynamic>>('/api/admin/mentors/$mentorProfileId/certificates');
    return _list(response);
  }

  // ---- Clients ----
  Future<List<Map<String, dynamic>>> getClients({String? search, int? fitnessGoalId, bool? isActive}) async {
    final response = await _dio.get<List<dynamic>>('/api/admin/clients', queryParameters: {
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
      'fitnessGoalId': ?fitnessGoalId,
      'isActive': ?isActive,
    });
    return _list(response);
  }

  // ---- Subscriptions ----
  Future<List<Map<String, dynamic>>> getSubscriptions({String? search, String? status}) async {
    final response = await _dio.get<List<dynamic>>('/api/admin/subscriptions', queryParameters: {
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
      if (status != null && status.isNotEmpty) 'status': status,
    });
    return _list(response);
  }

  Future<Map<String, dynamic>> cancelSubscription(int id, String reason) async {
    final response = await _dio.put<Map<String, dynamic>>(
      '/api/admin/subscriptions/$id/cancel',
      data: {'reason': reason},
    );
    return _map(response);
  }

  // ---- Reports ----
  Future<Map<String, dynamic>> getMentorReports({String? search, int? trainingTypeId, int? year, int? month}) async {
    final response = await _dio.get<Map<String, dynamic>>('/api/admin/reports/mentors', queryParameters: {
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
      'trainingTypeId': ?trainingTypeId,
      'year': ?year,
      'month': ?month,
    });
    return _map(response);
  }

  Future<Map<String, dynamic>> getMentorReportDetail(int mentorProfileId, {int? year, int? month}) async {
    final response = await _dio.get<Map<String, dynamic>>(
      '/api/admin/reports/mentors/$mentorProfileId',
      queryParameters: {
        'year': ?year,
        'month': ?month,
      },
    );
    return _map(response);
  }

  Future<Map<String, dynamic>> getClientReports({String? search, int? year, int? month}) async {
    final response = await _dio.get<Map<String, dynamic>>('/api/admin/reports/clients', queryParameters: {
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
      'year': ?year,
      'month': ?month,
    });
    return _map(response);
  }

  Future<Map<String, dynamic>> getClientReportDetail(int clientProfileId, {int? year, int? month}) async {
    final response = await _dio.get<Map<String, dynamic>>(
      '/api/admin/reports/clients/$clientProfileId',
      queryParameters: {
        'year': ?year,
        'month': ?month,
      },
    );
    return _map(response);
  }

  Future<Map<String, dynamic>> getOverviewReport() async {
    final response = await _dio.get<Map<String, dynamic>>('/api/admin/reports/overview');
    return _map(response);
  }

  // ---- Announcements ----
  Future<List<Map<String, dynamic>>> getAnnouncements({String? search}) async {
    final response = await _dio.get<List<dynamic>>('/api/admin/announcements', queryParameters: {
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
    });
    return _list(response);
  }

  Future<Map<String, dynamic>> createAnnouncement(String title, String content, String? targetRole) async {
    final response = await _dio.post<Map<String, dynamic>>('/api/admin/announcements', data: {
      'title': title,
      'content': content,
      'targetRole': targetRole,
    });
    return _map(response);
  }

  Future<Map<String, dynamic>> updateAnnouncement(int id, String title, String content) async {
    final response = await _dio.put<Map<String, dynamic>>('/api/admin/announcements/$id', data: {
      'title': title,
      'content': content,
    });
    return _map(response);
  }

  Future<void> deleteAnnouncement(int id) async {
    await _dio.delete<void>('/api/admin/announcements/$id');
  }
}
