import '../../core/network/dio_client.dart';
import '../models/training_plan.dart';
import '../models/training_session.dart';

abstract class TrainingPlanRepository {
  /// Throws [ApiException] with statusCode 404 when the client has no
  /// published plan yet (still awaiting the mentor / plan being prepared).
  Future<TrainingPlan> getMyCurrentPlan();

  Future<TrainingSessionItem> completeSession({
    required int planId,
    required int dayOfWeek,
    required int repetitions,
    String? note,
  });

  Future<List<TrainingSessionItem>> getSessions(int planId);
}

class ApiTrainingPlanRepository implements TrainingPlanRepository {
  ApiTrainingPlanRepository({DioClient? client})
      : _client = client ?? DioClient();

  final DioClient _client;

  @override
  Future<TrainingPlan> getMyCurrentPlan() async {
    final response = await _client.dio
        .get<Map<String, dynamic>>('/api/training-plans/my-current');
    return TrainingPlan.fromJson(response.data ?? const {});
  }

  @override
  Future<TrainingSessionItem> completeSession({
    required int planId,
    required int dayOfWeek,
    required int repetitions,
    String? note,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/training-plans/$planId/days/$dayOfWeek/sessions',
      data: {
        'repetitions': repetitions,
        if (note != null && note.trim().isNotEmpty) 'note': note.trim(),
      },
    );
    return TrainingSessionItem.fromJson(response.data ?? const {});
  }

  @override
  Future<List<TrainingSessionItem>> getSessions(int planId) async {
    final response = await _client.dio
        .get<List<dynamic>>('/api/training-plans/$planId/sessions');
    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(TrainingSessionItem.fromJson)
        .toList();
  }
}
