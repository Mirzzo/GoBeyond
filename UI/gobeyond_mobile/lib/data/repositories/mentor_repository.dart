import '../../core/network/dio_client.dart';
import '../models/mentor_model.dart';

class MentorRepository {
  MentorRepository(this._client);

  final DioClient _client;

  Future<List<MentorModel>> getMentors({
    String? search,
    String? category,
  }) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/mentors',
      queryParameters: {
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
        if (category != null && category.trim().isNotEmpty && category != 'All') 'category': category.trim(),
      },
    );

    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(MentorModel.fromJson)
        .toList();
  }

  Future<MentorModel> getMentorById(int mentorId) async {
    final response = await _client.dio.get<Map<String, dynamic>>('/api/mentors/$mentorId');
    if (response.data == null) {
      throw Exception('Empty mentor response.');
    }
    return MentorModel.fromJson(response.data!);
  }

  Future<List<Map<String, dynamic>>> getMentorReviews(int mentorId) async {
    final response = await _client.dio.get<List<dynamic>>('/api/reviews/mentor/$mentorId');
    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map((item) => Map<String, dynamic>.from(item))
        .toList();
  }

  Future<List<MentorModel>> getRecommendedMentors() async {
    final response = await _client.dio.get<List<dynamic>>('/api/mentors/recommended');
    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(MentorModel.fromJson)
        .toList();
  }
}
