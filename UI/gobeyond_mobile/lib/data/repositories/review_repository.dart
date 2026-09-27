import '../../core/network/dio_client.dart';
import '../models/review.dart';

abstract class ReviewRepository {
  Future<Review> createReview({
    required int subscriptionId,
    required int rating,
    required String comment,
  });

  Future<Review> updateReview({
    required int reviewId,
    required int rating,
    required String comment,
  });

  Future<void> deleteReview(int reviewId);
}

class ApiReviewRepository implements ReviewRepository {
  ApiReviewRepository({DioClient? client}) : _client = client ?? DioClient();

  final DioClient _client;

  @override
  Future<Review> createReview({
    required int subscriptionId,
    required int rating,
    required String comment,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/reviews',
      data: {
        'subscriptionId': subscriptionId,
        'rating': rating,
        'comment': comment,
      },
    );
    return Review.fromJson(response.data ?? const {});
  }

  @override
  Future<Review> updateReview({
    required int reviewId,
    required int rating,
    required String comment,
  }) async {
    final response = await _client.dio.put<Map<String, dynamic>>(
      '/api/reviews/$reviewId',
      data: {'rating': rating, 'comment': comment},
    );
    return Review.fromJson(response.data ?? const {});
  }

  @override
  Future<void> deleteReview(int reviewId) async {
    await _client.dio.delete<void>('/api/reviews/$reviewId');
  }
}
