import 'package:flutter_stripe/flutter_stripe.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/utils/stripe_error_messages.dart';

StripeException _exceptionFor(
  FailureCode code, {
  String? localizedMessage,
  String? message,
}) =>
    StripeException(
      error: LocalizedErrorMessage(
        code: code,
        localizedMessage: localizedMessage,
        message: message,
      ),
    );

void main() {
  group('stripePaymentErrorMessage', () {
    test('Canceled maps to a neutral Bosnian message, not the raw Stripe text', () {
      final message = stripePaymentErrorMessage(
        _exceptionFor(FailureCode.Canceled, message: 'The payment flow has been canceled'),
      );
      expect(message,
          'Plaćanje je otkazano. Možete pokušati ponovo kada budete spremni.');
    });

    test('Timeout maps to a clear retry message', () {
      final message = stripePaymentErrorMessage(_exceptionFor(FailureCode.Timeout));
      expect(message, contains('Isteklo je vrijeme'));
    });

    test('Failed prefers Stripe\'s own localizedMessage when present (e.g. card declined)', () {
      final message = stripePaymentErrorMessage(
        _exceptionFor(FailureCode.Failed, localizedMessage: 'Your card was declined.'),
      );
      expect(message, 'Your card was declined.');
    });

    test('Failed falls back to a Bosnian message when Stripe gives no localizedMessage', () {
      final message = stripePaymentErrorMessage(_exceptionFor(FailureCode.Failed));
      expect(message, contains('Plaćanje nije uspjelo'));
    });

    test('Unknown falls back to a generic Bosnian message', () {
      final message = stripePaymentErrorMessage(_exceptionFor(FailureCode.Unknown));
      expect(message, contains('neočekivane greške'));
    });

    test('Unknown prefers a plain message over the generic fallback', () {
      final message = stripePaymentErrorMessage(
        _exceptionFor(FailureCode.Unknown, message: 'Something odd happened'),
      );
      expect(message, 'Something odd happened');
    });
  });
}
