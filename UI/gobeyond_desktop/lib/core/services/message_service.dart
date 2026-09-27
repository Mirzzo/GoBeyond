import '../network/api_client.dart';

/// api-contract.md section 8 (Zajedničko — Poruke).
class MessageService {
  Future<List<Map<String, dynamic>>> getThreads({String? search}) async {
    final response = await ApiClient.instance.dio.get<List<dynamic>>('/api/messages/threads', queryParameters: {
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
    });
    return (response.data ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  Future<List<Map<String, dynamic>>> getMessages(int subscriptionId) async {
    final response = await ApiClient.instance.dio.get<List<dynamic>>('/api/messages/threads/$subscriptionId');
    return (response.data ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  Future<Map<String, dynamic>> sendMessage(int subscriptionId, String content) async {
    final response = await ApiClient.instance.dio.post<Map<String, dynamic>>(
      '/api/messages/threads/$subscriptionId',
      data: {'content': content},
    );
    return Map<String, dynamic>.from(response.data as Map);
  }
}
