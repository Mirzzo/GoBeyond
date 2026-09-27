import '../../core/network/dio_client.dart';
import '../models/subscription.dart';

class PaymentIntentResult {
  const PaymentIntentResult({
    required this.paymentId,
    required this.clientSecret,
    required this.publishableKey,
    required this.amount,
    required this.currency,
    required this.purpose,
  });

  final int paymentId;
  final String clientSecret;
  final String publishableKey;
  final num amount;
  final String currency;
  final String purpose;

  factory PaymentIntentResult.fromJson(Map<String, dynamic> json) =>
      PaymentIntentResult(
        paymentId: json['paymentId'] as int? ?? 0,
        clientSecret: json['clientSecret']?.toString() ?? '',
        publishableKey: json['publishableKey']?.toString() ?? '',
        amount: json['amount'] as num? ?? 0,
        currency: json['currency']?.toString() ?? 'USD',
        purpose: json['purpose']?.toString() ?? 'Initial',
      );
}

abstract class PaymentRepository {
  Future<PaymentIntentResult> createIntent(int subscriptionId);
  Future<Subscription> confirmPayment(int paymentId);
}

class ApiPaymentRepository implements PaymentRepository {
  ApiPaymentRepository({DioClient? client}) : _client = client ?? DioClient();

  final DioClient _client;

  @override
  Future<PaymentIntentResult> createIntent(int subscriptionId) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/payments/create-intent',
      data: {'subscriptionId': subscriptionId},
    );
    return PaymentIntentResult.fromJson(response.data ?? const {});
  }

  @override
  Future<Subscription> confirmPayment(int paymentId) async {
    final response = await _client.dio
        .post<Map<String, dynamic>>('/api/payments/$paymentId/confirm');
    return Subscription.fromJson(response.data ?? const {});
  }
}
