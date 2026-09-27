import '../network/api_client.dart';

/// api-contract.md section 8 (Zajedničko — Notifikacije).
class NotificationService {
  Future<List<Map<String, dynamic>>> getNotifications({bool? unreadOnly, String? search}) async {
    final response = await ApiClient.instance.dio.get<List<dynamic>>('/api/notifications', queryParameters: {
      'unreadOnly': ?unreadOnly,
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
    });
    return (response.data ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  Future<void> markRead(int id) async {
    await ApiClient.instance.dio.put<void>('/api/notifications/$id/read');
  }

  Future<void> markAllRead() async {
    await ApiClient.instance.dio.put<void>('/api/notifications/read-all');
  }

  Future<int> unreadCount() async {
    final response = await ApiClient.instance.dio.get<Map<String, dynamic>>('/api/notifications/unread-count');
    return (response.data?['count'] as num?)?.toInt() ?? 0;
  }
}
