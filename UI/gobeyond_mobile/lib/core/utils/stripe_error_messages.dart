import 'package:flutter_stripe/flutter_stripe.dart';

/// Maps a [StripeException] thrown by `presentPaymentSheet()` /
/// `initPaymentSheet()` to a clear Bosnian message for the payment screens.
///
/// `flutter_stripe` 13.1 only exposes four [FailureCode] values (see
/// `stripe_platform_interface-13.1.0/lib/src/models/errors.dart`): `Failed`,
/// `Canceled`, `Timeout` and `Unknown`. Every one of them is handled here so
/// no PaymentSheet failure ever falls through to a raw/English message.
String stripePaymentErrorMessage(StripeException error) {
  final detail = error.error.localizedMessage ?? error.error.message;

  switch (error.error.code) {
    case FailureCode.Canceled:
      // The user closed the sheet themselves (back button / swipe down) -
      // not a failure, so the wording stays neutral rather than alarming.
      return 'Plaćanje je otkazano. Možete pokušati ponovo kada budete spremni.';
    case FailureCode.Timeout:
      return 'Isteklo je vrijeme za potvrdu plaćanja. Provjerite internet '
          'konekciju i pokušajte ponovo.';
    case FailureCode.Failed:
      // e.g. card declined, insufficient funds, incorrect CVC - Stripe's own
      // localizedMessage is already user-facing, so prefer it when present.
      return detail ??
          'Plaćanje nije uspjelo. Provjerite podatke kartice i pokušajte '
              'ponovo.';
    case FailureCode.Unknown:
      return detail ??
          'Došlo je do neočekivane greške pri plaćanju. Pokušajte ponovo.';
  }
}
