import '../../core/network/dio_client.dart';

class SubscriptionRepository {
  SubscriptionRepository(this._client);

  final DioClient _client;

  Future<void> getSubscription() async {
    await _client.dio.get('/api/subscriptions/my');
  }
}
