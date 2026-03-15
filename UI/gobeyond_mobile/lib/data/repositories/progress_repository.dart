import '../../core/network/dio_client.dart';
import '../models/progress_model.dart';

class ProgressRepository {
  ProgressRepository(this._client);

  final DioClient _client;

  Future<ProgressHistoryModel> getProgressHistory({String? search}) async {
    final response = await _client.dio.get<Map<String, dynamic>>(
      '/api/progress',
      queryParameters: {
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
      },
    );

    if (response.data == null) {
      throw Exception('Empty progress response.');
    }

    return ProgressHistoryModel.fromJson(response.data!);
  }

  Future<void> createProgressEntry(Map<String, dynamic> payload) async {
    await _client.dio.post<void>('/api/progress', data: payload);
  }

  Future<void> uploadPhoto(String photoUrl) async {
    await _client.dio.post<void>(
      '/api/progress/photo',
      data: {'photoUrl': photoUrl},
    );
  }
}
