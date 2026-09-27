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

  Future<SubscriptionModel> createSubscription(
      Map<String, dynamic> payload) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/subscriptions',
      data: payload,
    );

    final subscription =
        response.data?['subscription'] as Map<String, dynamic>?;
    if (subscription == null) {
      throw Exception('Empty subscription response.');
    }

    return SubscriptionModel.fromJson(subscription);
  }

  Future<Map<String, dynamic>> getPaymentConfig() async {
    final response =
        await _client.dio.get<Map<String, dynamic>>('/api/payments/config');
    return response.data ?? const {};
  }

  Future<Map<String, dynamic>> createPaymentIntent(int subscriptionId) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/payments/create-intent',
      data: {'subscriptionId': subscriptionId},
    );
    return response.data ??
        (throw StateError('Server nije vratio podatke za plaćanje.'));
  }

  Future<void> confirmDemoPayment(int paymentId) async {
    await _client.dio.post<void>('/api/payments/$paymentId/confirm-demo');
  }

  Future<void> refreshPayment(int paymentId) async {
    await _client.dio.post<void>('/api/payments/$paymentId/refresh');
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
