import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_desktop/presentation/screens/admin/admin_subscriptions_screen.dart';

/// admin-cancel-awaiting-mentor-no-refund (desktop part): an AwaitingMentor
/// subscription was already paid for by the client but never accepted by
/// the mentor. The backend refunds it on admin cancel, so the
/// OTKAŽI confirmation dialog must say the client's payment will be
/// refunded — otherwise the admin cancels believing (as the old text
/// implied) that only a notification is sent.
void main() {
  group('subscriptionCancelWarning', () {
    test('mentions the refund for an AwaitingMentor subscription', () {
      final warning = subscriptionCancelWarning(
        status: 'AwaitingMentor',
        clientFullName: 'Amina Hodžić',
        mentorFullName: 'Kenan Omerović',
      );
      expect(warning, contains('Amina Hodžić'));
      expect(warning, contains('Kenan Omerović'));
      expect(warning, contains('uplata će biti vraćena'));
    });

    test('does not mention a refund for an Active subscription', () {
      final warning = subscriptionCancelWarning(
        status: 'Active',
        clientFullName: 'Amina Hodžić',
        mentorFullName: 'Kenan Omerović',
      );
      expect(warning, isNot(contains('vraćena')));
    });

    test('does not mention a refund for a PendingPayment subscription', () {
      final warning = subscriptionCancelWarning(
        status: 'PendingPayment',
        clientFullName: 'Amina Hodžić',
        mentorFullName: 'Kenan Omerović',
      );
      expect(warning, isNot(contains('vraćena')));
    });
  });
}
