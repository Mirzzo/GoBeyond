import '../../core/network/dio_client.dart';

abstract class ActivityRepository {
  Future<void> sendHeartbeat();
}

class ApiActivityRepository implements ActivityRepository {
  ApiActivityRepository({DioClient? client}) : _client = client ?? DioClient();

  final DioClient _client;

  @override
  Future<void> sendHeartbeat() async {
    await _client.dio.post<void>('/api/activity/heartbeat');
  }
}
