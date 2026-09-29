import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/data/repositories/payment_repository.dart';

void main() {
  group('PaymentIntentResult.fromJson', () {
    // Same shape as a real POST /api/payments/create-intent response; the
    // Stripe identifiers are placeholders, never real keys or secrets.
    const liveResponseJson = {
      'paymentId': 1011,
      'clientSecret':
          'pi_test_placeholder_secret_placeholder',
      'publishableKey':
          'pk_test_placeholder',
      'amount': 18.50,
      'currency': 'usd',
      'purpose': 'Initial',
    };

    test('parses every field from a real create-intent response', () {
      final result = PaymentIntentResult.fromJson(liveResponseJson);

      expect(result.paymentId, 1011);
      expect(result.clientSecret, startsWith('pi_test_placeholder_secret_'));
      expect(result.publishableKey, startsWith('pk_test_'));
      expect(result.amount, 18.50);
      expect(result.currency, 'usd');
      expect(result.purpose, 'Initial');
    });

    test('a Renewal response parses the same way', () {
      final json = {...liveResponseJson, 'purpose': 'Renewal', 'paymentId': 1012};
      final result = PaymentIntentResult.fromJson(json);
      expect(result.purpose, 'Renewal');
      expect(result.paymentId, 1012);
    });

    test('missing/null fields fall back to safe defaults instead of throwing', () {
      final result = PaymentIntentResult.fromJson(const {});
      expect(result.paymentId, 0);
      expect(result.clientSecret, '');
      expect(result.publishableKey, '');
      expect(result.amount, 0);
      expect(result.currency, 'USD');
      expect(result.purpose, 'Initial');
    });
  });
}
