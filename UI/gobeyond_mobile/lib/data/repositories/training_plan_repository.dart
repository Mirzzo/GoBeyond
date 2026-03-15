import '../../core/network/dio_client.dart';
import '../models/training_plan_model.dart';

class TrainingPlanRepository {
  TrainingPlanRepository(this._client);

  final DioClient _client;

  Future<TrainingPlanModel> getCurrentPlan() async {
    final response = await _client.dio.get<Map<String, dynamic>>('/api/training-plans/my-current');
    if (response.data == null) {
      throw Exception('Empty training plan response.');
    }

    return TrainingPlanModel.fromJson(response.data!);
  }
}
