import 'package:flutter_stripe/flutter_stripe.dart';

/// Maps a [StripeException] thrown by `presentPaymentSheet()` /
/// `initPaymentSheet()` to a clear Bosnian message for the payment screens.
///
/// Never returns Stripe's own `localizedMessage`/`message` - those come from
/// the Stripe SDK/API in the device's language (English on a typical device,
/// since Stripe has no Bosnian locale), never Bosnian. Every failure is
/// mapped to a Bosnian message here instead: a known decline/error code gets
/// a specific message, anything else falls back to a generic one.
///
/// `flutter_stripe` 13.1 only exposes four [FailureCode] values (see
/// `stripe_platform_interface-13.1.0/lib/src/models/errors.dart`): `Failed`,
/// `Canceled`, `Timeout` and `Unknown`. Every one of them is handled here so
/// no PaymentSheet failure ever falls through to a raw/English message.
String stripePaymentErrorMessage(StripeException error) {
  switch (error.error.code) {
    case FailureCode.Canceled:
      // The user closed the sheet themselves (back button / swipe down) -
      // not a failure, so the wording stays neutral rather than alarming.
      return 'Plaćanje je otkazano. Možete pokušati ponovo kada budete spremni.';
    case FailureCode.Timeout:
      return 'Isteklo je vrijeme za potvrdu plaćanja. Provjerite internet '
          'konekciju i pokušajte ponovo.';
    case FailureCode.Failed:
      // e.g. card declined, insufficient funds, incorrect CVC.
      return _declineMessage(error) ??
          'Plaćanje nije uspjelo. Provjerite podatke kartice i pokušajte '
              'ponovo.';
    case FailureCode.Unknown:
      return _declineMessage(error) ??
          'Došlo je do neočekivane greške pri plaćanju. Pokušajte ponovo.';
  }
}

/// Maps Stripe's `declineCode`/`stripeErrorCode` to a Bosnian message.
/// Returns null for an unrecognized code, so the caller's generic Bosnian
/// fallback applies instead of falling through to Stripe's English text.
String? _declineMessage(StripeException error) {
  final code = error.error.declineCode ?? error.error.stripeErrorCode;
  switch (code) {
    case 'insufficient_funds':
      return 'Na kartici nema dovoljno sredstava.';
    case 'card_declined':
    case 'generic_decline':
      return 'Kartica je odbijena. Pokušajte s drugom karticom.';
    case 'expired_card':
      return 'Kartici je istekao rok važenja.';
    case 'incorrect_cvc':
    case 'invalid_cvc':
      return 'Netačan CVC kod kartice.';
    case 'incorrect_number':
    case 'invalid_number':
      return 'Netačan broj kartice.';
    case 'processing_error':
      return 'Greška pri obradi plaćanja. Pokušajte ponovo.';
    case 'payment_intent_authentication_failure':
      return 'Potvrda plaćanja (3D Secure) nije uspjela. Pokušajte ponovo.';
    case 'lost_card':
    case 'stolen_card':
      return 'Kartica je odbijena. Kontaktirajte svoju banku.';
    default:
      return null;
  }
}
