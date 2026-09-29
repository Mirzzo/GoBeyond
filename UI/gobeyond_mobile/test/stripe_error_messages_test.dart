import 'package:flutter_stripe/flutter_stripe.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/utils/stripe_error_messages.dart';

StripeException _exceptionFor(
  FailureCode code, {
  String? localizedMessage,
  String? message,
  String? declineCode,
  String? stripeErrorCode,
}) =>
    StripeException(
      error: LocalizedErrorMessage(
        code: code,
        localizedMessage: localizedMessage,
        message: message,
        declineCode: declineCode,
        stripeErrorCode: stripeErrorCode,
      ),
    );

void main() {
  group('stripePaymentErrorMessage', () {
    test('Canceled maps to a neutral Bosnian message, not the raw Stripe text',
        () {
      final message = stripePaymentErrorMessage(
        _exceptionFor(FailureCode.Canceled,
            message: 'The payment flow has been canceled'),
      );
      expect(message,
          'Plaćanje je otkazano. Možete pokušati ponovo kada budete spremni.');
    });

    test('Timeout maps to a clear retry message', () {
      final message =
          stripePaymentErrorMessage(_exceptionFor(FailureCode.Timeout));
      expect(message, contains('Isteklo je vrijeme'));
    });

    test(
        'Failed never surfaces Stripe\'s own English localizedMessage, even '
        'when Stripe provides one', () {
      final message = stripePaymentErrorMessage(
        _exceptionFor(FailureCode.Failed,
            localizedMessage: 'Your card was declined.'),
      );
      expect(message, isNot('Your card was declined.'));
      expect(message, contains('Plaćanje nije uspjelo'));
    });

    test(
        'Failed falls back to a Bosnian message when Stripe gives no decline code',
        () {
      final message =
          stripePaymentErrorMessage(_exceptionFor(FailureCode.Failed));
      expect(message, contains('Plaćanje nije uspjelo'));
    });

    test(
        'Failed with declineCode insufficient_funds maps to a specific Bosnian message',
        () {
      final message = stripePaymentErrorMessage(
        _exceptionFor(FailureCode.Failed,
            localizedMessage: 'Your card has insufficient funds.',
            declineCode: 'insufficient_funds'),
      );
      expect(message, 'Na kartici nema dovoljno sredstava.');
    });

    test(
        'Failed with declineCode card_declined maps to a specific Bosnian message',
        () {
      final message = stripePaymentErrorMessage(
        _exceptionFor(FailureCode.Failed, declineCode: 'card_declined'),
      );
      expect(message, 'Kartica je odbijena. Pokušajte s drugom karticom.');
    });

    test(
        'Failed with declineCode expired_card maps to a specific Bosnian message',
        () {
      final message = stripePaymentErrorMessage(
        _exceptionFor(FailureCode.Failed, declineCode: 'expired_card'),
      );
      expect(message, 'Kartici je istekao rok važenja.');
    });

    test(
        'Failed with stripeErrorCode incorrect_cvc maps to a specific Bosnian message',
        () {
      final message = stripePaymentErrorMessage(
        _exceptionFor(FailureCode.Failed, stripeErrorCode: 'incorrect_cvc'),
      );
      expect(message, 'Netačan CVC kod kartice.');
    });

    test('Unknown falls back to a generic Bosnian message', () {
      final message =
          stripePaymentErrorMessage(_exceptionFor(FailureCode.Unknown));
      expect(message, contains('neočekivane greške'));
    });

    test('Unknown never surfaces Stripe\'s own English message', () {
      final message = stripePaymentErrorMessage(
        _exceptionFor(FailureCode.Unknown, message: 'Something odd happened'),
      );
      expect(message, isNot('Something odd happened'));
      expect(message, contains('neočekivane greške'));
    });
  });
}
