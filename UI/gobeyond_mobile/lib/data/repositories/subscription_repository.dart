import '../../core/network/dio_client.dart';
import '../models/questionnaire.dart';
import '../models/subscription.dart';

abstract class SubscriptionRepository {
  Future<Subscription> createSubscription({
    required int mentorProfileId,
    required Questionnaire questionnaire,
  });

  Future<List<Subscription>> getMySubscriptions({String? status});

  Future<Subscription> getSubscriptionDetail(int id);

  Future<Subscription> cancelSubscription(int id);
}

class ApiSubscriptionRepository implements SubscriptionRepository {
  ApiSubscriptionRepository({DioClient? client})
      : _client = client ?? DioClient();

  final DioClient _client;

  @override
  Future<Subscription> createSubscription({
    required int mentorProfileId,
    required Questionnaire questionnaire,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/subscriptions',
      data: {
        'mentorProfileId': mentorProfileId,
        'questionnaire': questionnaire.toJson(),
      },
    );
    return Subscription.fromJson(response.data ?? const {});
  }

  @override
  Future<List<Subscription>> getMySubscriptions({String? status}) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/subscriptions/my',
      queryParameters: {if (status != null) 'status': status},
    );
    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(Subscription.fromJson)
        .toList();
  }

  @override
  Future<Subscription> getSubscriptionDetail(int id) async {
    final response = await _client.dio
        .get<Map<String, dynamic>>('/api/subscriptions/my/$id');
    return Subscription.fromJson(response.data ?? const {});
  }

  @override
  Future<Subscription> cancelSubscription(int id) async {
    final response = await _client.dio
        .post<Map<String, dynamic>>('/api/subscriptions/$id/cancel');
    return Subscription.fromJson(response.data ?? const {});
  }
}
