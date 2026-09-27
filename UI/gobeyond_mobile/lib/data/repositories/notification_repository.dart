import '../../core/network/dio_client.dart';
import '../models/notification_item.dart';

abstract class NotificationRepository {
  Future<List<NotificationItem>> getNotifications({
    bool unreadOnly = false,
    String? search,
  });
  Future<void> markRead(int id);
  Future<void> markAllRead();
  Future<int> getUnreadCount();
}

class ApiNotificationRepository implements NotificationRepository {
  ApiNotificationRepository({DioClient? client})
      : _client = client ?? DioClient();

  final DioClient _client;

  @override
  Future<List<NotificationItem>> getNotifications({
    bool unreadOnly = false,
    String? search,
  }) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/notifications',
      queryParameters: {
        if (unreadOnly) 'unreadOnly': true,
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
      },
    );
    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(NotificationItem.fromJson)
        .toList();
  }

  @override
  Future<void> markRead(int id) async {
    await _client.dio.put<void>('/api/notifications/$id/read');
  }

  @override
  Future<void> markAllRead() async {
    await _client.dio.put<void>('/api/notifications/read-all');
  }

  @override
  Future<int> getUnreadCount() async {
    final response = await _client.dio
        .get<Map<String, dynamic>>('/api/notifications/unread-count');
    return response.data?['count'] as int? ?? 0;
  }
}
