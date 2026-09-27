import '../../core/network/dio_client.dart';
import '../models/message_thread.dart';

abstract class MessageRepository {
  Future<List<MessageThread>> getThreads({String? search});
  Future<List<MessageItem>> getThreadMessages(int subscriptionId);
  Future<MessageItem> sendMessage(int subscriptionId, String content);
}

class ApiMessageRepository implements MessageRepository {
  ApiMessageRepository({DioClient? client}) : _client = client ?? DioClient();

  final DioClient _client;

  @override
  Future<List<MessageThread>> getThreads({String? search}) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/messages/threads',
      queryParameters: {
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
      },
    );
    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(MessageThread.fromJson)
        .toList();
  }

  @override
  Future<List<MessageItem>> getThreadMessages(int subscriptionId) async {
    final response = await _client.dio
        .get<List<dynamic>>('/api/messages/threads/$subscriptionId');
    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(MessageItem.fromJson)
        .toList();
  }

  @override
  Future<MessageItem> sendMessage(int subscriptionId, String content) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/messages/threads/$subscriptionId',
      data: {'content': content},
    );
    return MessageItem.fromJson(response.data ?? const {});
  }
}
