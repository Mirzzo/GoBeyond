import '../../core/network/dio_client.dart';
import '../models/subscription_model.dart';

class SubscriptionRepository {
  SubscriptionRepository(this._client);

  final DioClient _client;

  Future<List<SubscriptionModel>> getMySubscriptions({String? search}) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/subscriptions/my',
      queryParameters: {
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
      },
    );

    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(SubscriptionModel.fromJson)
        .toList();
  }

  Future<SubscriptionModel> createSubscription(Map<String, dynamic> payload) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/subscriptions',
      data: payload,
    );

    final subscription = response.data?['subscription'] as Map<String, dynamic>?;
    if (subscription == null) {
      throw Exception('Empty subscription response.');
    }

    return SubscriptionModel.fromJson(subscription);
  }

  Future<SubscriptionModel> confirmPayment(int subscriptionId) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/payments/create-intent',
      data: {'subscriptionId': subscriptionId},
    );

    final subscription = response.data?['subscription'] as Map<String, dynamic>?;
    if (subscription == null) {
      throw Exception('Empty payment response.');
    }

    return SubscriptionModel.fromJson(subscription);
  }

  Future<SubscriptionModel> cancelSubscription(int subscriptionId) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/subscriptions/$subscriptionId/cancel',
    );

    if (response.data == null) {
      throw Exception('Empty cancellation response.');
    }

    return SubscriptionModel.fromJson(response.data!);
  }
}
