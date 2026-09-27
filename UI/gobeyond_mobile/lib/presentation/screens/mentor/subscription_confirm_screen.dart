import 'package:flutter/material.dart';
import 'package:flutter_stripe/flutter_stripe.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/mentor_summary.dart';
import '../../../data/models/questionnaire.dart';
import '../../../data/repositories/payment_repository.dart';
import '../../../data/repositories/subscription_repository.dart';
import '../../widgets/app_dialogs.dart';
import '../../widgets/app_modal_page.dart';
import '../../widgets/app_network_image.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/primary_button.dart';
import '../home/home_screen.dart';

/// Confirmation step before payment (mockup 09 flow): mentor, price, 30-day
/// period and the note that the mentor still needs to accept, then drives
/// the Stripe PaymentSheet end to end.
class SubscriptionConfirmScreen extends StatefulWidget {
  const SubscriptionConfirmScreen({
    super.key,
    required this.mentor,
    required this.questionnaire,
    this.subscriptionRepository,
    this.paymentRepository,
  });

  final MentorDetail mentor;
  final Questionnaire questionnaire;
  final SubscriptionRepository? subscriptionRepository;
  final PaymentRepository? paymentRepository;

  @override
  State<SubscriptionConfirmScreen> createState() =>
      _SubscriptionConfirmScreenState();
}

class _SubscriptionConfirmScreenState extends State<SubscriptionConfirmScreen> {
  late final SubscriptionRepository _subscriptionRepository =
      widget.subscriptionRepository ?? ApiSubscriptionRepository();
  late final PaymentRepository _paymentRepository =
      widget.paymentRepository ?? ApiPaymentRepository();

  bool _processing = false;
  String? _statusMessage;
  String? _errorMessage;

  Future<void> _confirmAndPay() async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Potvrda pretplate',
      message:
          'Pretplaćujete se na mentora ${widget.mentor.fullName} na 30 dana za '
          '${Formatters.price(widget.mentor.monthlyPrice, widget.mentor.currency)}. '
          'Mentor mora prihvatiti vaš zahtjev prije početka saradnje. Nastaviti na plaćanje?',
      confirmLabel: 'POTVRDI',
    );
    if (!confirmed) return;

    setState(() {
      _processing = true;
      _errorMessage = null;
      _statusMessage = 'Kreiranje pretplate...';
    });

    try {
      final subscription = await _subscriptionRepository.createSubscription(
        mentorProfileId: widget.mentor.mentorProfileId,
        questionnaire: widget.questionnaire,
      );
      if (!mounted) return;

      setState(() => _statusMessage = 'Priprema plaćanja...');
      final intent = await _paymentRepository.createIntent(subscription.id);
      if (!mounted) return;

      Stripe.publishableKey = intent.publishableKey;
      await Stripe.instance.applySettings();
      if (!mounted) return;

      setState(() => _statusMessage = 'Otvaranje forme za plaćanje...');
      await Stripe.instance.initPaymentSheet(
        paymentSheetParameters: SetupPaymentSheetParameters(
          paymentIntentClientSecret: intent.clientSecret,
          merchantDisplayName: 'GoBeyond',
        ),
      );
      await Stripe.instance.presentPaymentSheet();
      if (!mounted) return;

      setState(() => _statusMessage = 'Potvrđivanje uplate...');
      await _paymentRepository.confirmPayment(intent.paymentId);

      if (!mounted) return;
      await showInfoDialog(
        context,
        title: 'Uspješno',
        message:
            'Plaćanje je uspješno. Mentor je dobio vaš zahtjev za saradnju.',
      );
      if (!mounted) return;
      // Collapse the whole KUPI PLAN flow (list -> detail -> questionnaire ->
      // confirm) back to a fresh Home screen.
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => const HomeScreen()),
        (route) => false,
      );
    } on StripeException catch (error) {
      if (!mounted) return;
      final isCanceled = error.error.code == FailureCode.Canceled;
      setState(() {
        _errorMessage = isCanceled
            ? 'Plaćanje je otkazano. Možete pokušati ponovo kada budete spremni.'
            : (error.error.localizedMessage ??
                error.error.message ??
                'Plaćanje nije uspjelo. Pokušajte ponovo.');
      });
    } catch (error) {
      if (!mounted) return;
      final apiError = ApiException.from(error);
      setState(() => _errorMessage = _describeError(apiError));
    } finally {
      if (mounted) setState(() => _processing = false);
    }
  }

  /// The questionnaire fields aren't editable on this screen (they were
  /// collected on the previous one), so a 400 with nested `questionnaire.*`
  /// field errors has nowhere to render per-field — fold every message into
  /// the single banner instead of silently showing just the generic
  /// "Provjerite unesene podatke." summary.
  String _describeError(ApiException error) {
    if (error.errors.isEmpty) return error.message;
    final details =
        error.errors.values.expand((messages) => messages).join(' ');
    return '${error.message} $details'.trim();
  }

  @override
  Widget build(BuildContext context) {
    final mentor = widget.mentor;
    return AppModalPage(
      title: 'Potvrda pretplate',
      body: ListView(
        children: [
          AppPanel(
            child: Row(
              children: [
                AppNetworkImage(
                  url: mentor.profileImageUrl,
                  width: 72,
                  height: 72,
                  borderRadius: 36,
                  yellowBorder: true,
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(mentor.fullName,
                          style: const TextStyle(
                              fontWeight: FontWeight.w800, fontSize: 16)),
                      const SizedBox(height: 4),
                      Text(mentor.trainingTypeName,
                          style: const TextStyle(color: AppTheme.textMuted)),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          AppPanel(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _InfoRow(
                  icon: Icons.payments_rounded,
                  label: 'Cijena',
                  value: Formatters.price(mentor.monthlyPrice, mentor.currency),
                ),
                const SizedBox(height: 10),
                const _InfoRow(
                  icon: Icons.calendar_month_rounded,
                  label: 'Period saradnje',
                  value: '30 dana',
                ),
                const SizedBox(height: 14),
                const Text(
                  'Napomena: mentor mora prihvatiti vaš zahtjev prije nego što saradnja i plan treninga postanu aktivni.',
                  style: TextStyle(color: AppTheme.textMuted, fontSize: 12.5),
                ),
              ],
            ),
          ),
          if (_statusMessage != null && _processing) ...[
            const SizedBox(height: 16),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
                const SizedBox(width: 10),
                Text(_statusMessage!,
                    style: const TextStyle(color: AppTheme.textMuted)),
              ],
            ),
          ],
          if (_errorMessage != null) ...[
            const SizedBox(height: 16),
            Text(_errorMessage!,
                textAlign: TextAlign.center,
                style: const TextStyle(color: AppTheme.danger)),
          ],
          const SizedBox(height: 24),
          PrimaryButton(
            label: 'POTVRDI',
            isLoading: _processing,
            onPressed: _confirmAndPay,
          ),
          const SizedBox(height: 12),
          OutlinedButton(
            onPressed: _processing ? null : () => Navigator.of(context).pop(),
            child: const SizedBox(
              width: double.infinity,
              child: Text('ODUSTANI', textAlign: TextAlign.center),
            ),
          ),
        ],
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow(
      {required this.icon, required this.label, required this.value});

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(icon, color: AppTheme.accent, size: 20),
        const SizedBox(width: 10),
        Text('$label:', style: const TextStyle(color: AppTheme.textMuted)),
        const SizedBox(width: 6),
        Expanded(
          child: Text(
            value,
            style: const TextStyle(fontWeight: FontWeight.w700),
            overflow: TextOverflow.ellipsis,
          ),
        ),
      ],
    );
  }
}
