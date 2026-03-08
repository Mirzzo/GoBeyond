import '../../core/network/dio_client.dart';

class ProgressRepository {
  ProgressRepository(this._client);

  final DioClient _client;

  Future<void> getProgressHistory() async {
    await _client.dio.get('/api/progress');
  }
}
