import '../../core/network/dio_client.dart';

class TrainingPlanRepository {
  TrainingPlanRepository(this._client);

  final DioClient _client;

  Future<void> getCurrentPlan() async {
    await _client.dio.get('/api/training-plans/my-current');
  }
}
