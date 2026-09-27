import '../../core/network/dio_client.dart';
import '../models/mentor_summary.dart';
import '../models/recommendation.dart';
import '../models/review.dart';

abstract class MentorRepository {
  Future<List<MentorSummary>> getMentors({
    int? trainingTypeId,
    String? search,
    String sortBy = 'rating',
    String sortDirection = 'desc',
  });

  Future<MentorDetail> getMentorById(int mentorProfileId);

  Future<List<Review>> getMentorReviews(int mentorProfileId);

  Future<List<MentorSummary>> getSimilarMentors(int mentorProfileId,
      {int take = 3});

  Future<List<MentorRecommendation>> getRecommendedMentors({int take = 5});
}

class ApiMentorRepository implements MentorRepository {
  ApiMentorRepository({DioClient? client}) : _client = client ?? DioClient();

  final DioClient _client;

  @override
  Future<List<MentorSummary>> getMentors({
    int? trainingTypeId,
    String? search,
    String sortBy = 'rating',
    String sortDirection = 'desc',
  }) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/mentors',
      queryParameters: {
        if (trainingTypeId != null) 'trainingTypeId': trainingTypeId,
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
        'sortBy': sortBy,
        'sortDirection': sortDirection,
      },
    );
    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(MentorSummary.fromJson)
        .toList();
  }

  @override
  Future<MentorDetail> getMentorById(int mentorProfileId) async {
    final response = await _client.dio
        .get<Map<String, dynamic>>('/api/mentors/$mentorProfileId');
    return MentorDetail.fromJson(response.data ?? const {});
  }

  @override
  Future<List<Review>> getMentorReviews(int mentorProfileId) async {
    final response = await _client.dio
        .get<List<dynamic>>('/api/mentors/$mentorProfileId/reviews');
    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(Review.fromJson)
        .toList();
  }

  @override
  Future<List<MentorSummary>> getSimilarMentors(int mentorProfileId,
      {int take = 3}) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/mentors/$mentorProfileId/similar',
      queryParameters: {'take': take},
    );
    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(MentorSummary.fromJson)
        .toList();
  }

  @override
  Future<List<MentorRecommendation>> getRecommendedMentors(
      {int take = 5}) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/recommendations/mentors',
      queryParameters: {'take': take},
    );
    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(MentorRecommendation.fromJson)
        .toList();
  }
}
