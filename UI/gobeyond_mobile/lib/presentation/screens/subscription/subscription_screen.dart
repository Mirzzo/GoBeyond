import 'package:flutter/material.dart';
import 'package:flutter_stripe/flutter_stripe.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/network/dio_client.dart';
import '../../../core/theme/app_theme.dart';
import '../../../data/models/mentor_model.dart';
import '../../../data/models/subscription_model.dart';
import '../../../data/repositories/subscription_repository.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/section_header.dart';

class SubscriptionScreen extends StatefulWidget {
  const SubscriptionScreen({
    super.key,
    this.mentor,
    this.questionnaireAnswers,
  });

  final MentorModel? mentor;
  final Map<String, String>? questionnaireAnswers;

  @override
  State<SubscriptionScreen> createState() => _SubscriptionScreenState();
}

class _SubscriptionScreenState extends State<SubscriptionScreen> {
  final SubscriptionRepository _repository =
      SubscriptionRepository(DioClient());
  SubscriptionModel? _subscription;
  bool _isLoading = true;
  bool _isBusy = false;
  String? _errorMessage;
  String _paymentMode = '';
  String? _publishableKey;

  bool get _isOnboardingFlow =>
      widget.mentor != null && widget.questionnaireAnswers != null;

  @override
  void initState() {
    super.initState();
    _loadPaymentConfig();
    if (_isOnboardingFlow) {
      _isLoading = false;
    } else {
      _loadCurrentSubscription();
    }
  }

  Future<void> _loadPaymentConfig() async {
    try {
      final config = await _repository.getPaymentConfig();
      if (mounted)
        setState(() {
          _paymentMode = config['mode']?.toString() ?? '';
          _publishableKey = config['publishableKey']?.toString();
        });
    } catch (error) {
      if (mounted)
        setState(() => _errorMessage = 'Način plaćanja nije dostupan: $error');
    }
  }

  Future<void> _loadCurrentSubscription() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final subscriptions = await _repository.getMySubscriptions();
      final activeSubscription = subscriptions
          .cast<SubscriptionModel?>()
          .firstWhere(
            (item) => item?.status == 'Active',
            orElse: () => subscriptions.isEmpty ? null : subscriptions.first,
          );

      if (!mounted) {
        return;
      }

      setState(() {
        _subscription = activeSubscription;
      });
    } catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _errorMessage = error.toString();
      });
    } finally {
      if (mounted) {
        setState(() => _isLoading = false);
      }
    }
  }

  Future<void> _startSubscription() async {
    final mentor = widget.mentor;
    final answers = widget.questionnaireAnswers;
    if (mentor == null || answers == null) {
      return;
    }

    final isDemo = _paymentMode.toLowerCase() == 'demo';
    final confirmed = await showDialog<bool>(
          context: context,
          builder: (context) => AlertDialog(
            title: Text(isDemo
                ? 'Potvrditi demo plaćanje?'
                : 'Nastaviti na Stripe plaćanje?'),
            content: Text(isDemo
                ? 'Ovo je simulacija plaćanja. Kartica se ne naplaćuje. Zahtjev za saradnju šalje se mentoru nakon potvrde.'
                : 'Stripe će otvoriti siguran unos kartice. Pretplata postaje aktivna tek nakon potvrde plaćanja.'),
            actions: [
              TextButton(
                  onPressed: () => Navigator.pop(context, false),
                  child: const Text('Odustani')),
              ElevatedButton(
                  onPressed: () => Navigator.pop(context, true),
                  child: const Text('Nastavi')),
            ],
          ),
        ) ??
        false;
    if (!confirmed || !mounted) return;
    setState(() => _isBusy = true);
    try {
      final created = _subscription ??
          await _repository.createSubscription({
            'mentorId': mentor.id,
            ...answers,
          });
      if (mounted) setState(() => _subscription = created);
      final intent = await _repository.createPaymentIntent(created.id);
      final payment = intent['payment'] as Map<String, dynamic>?;
      final paymentId = payment?['id'] as int?;
      if (paymentId == null)
        throw StateError('Server nije vratio identitet plaćanja.');
      final mode = intent['mode']?.toString() ?? _paymentMode;
      if (mode.toLowerCase() == 'demo') {
        await _repository.confirmDemoPayment(paymentId);
      } else if (mode.toLowerCase() == 'stripe') {
        final key = intent['publishableKey']?.toString() ??
            _publishableKey ??
            AppConstants.stripePublishableKey;
        final secret = intent['clientSecret']?.toString() ?? '';
        if (key.isEmpty || secret.isEmpty)
          throw StateError('Stripe konfiguracija nije potpuna.');
        Stripe.publishableKey = key;
        await Stripe.instance.applySettings();
        await Stripe.instance.initPaymentSheet(
          paymentSheetParameters: SetupPaymentSheetParameters(
            paymentIntentClientSecret: secret,
            merchantDisplayName: 'GoBeyond',
          ),
        );
        await Stripe.instance.presentPaymentSheet();
        await _repository.refreshPayment(paymentId);
      } else {
        throw StateError('Nepoznat način plaćanja.');
      }
      final subscriptions = await _repository.getMySubscriptions();
      final updated =
          subscriptions.where((item) => item.id == created.id).firstOrNull;

      if (!mounted) {
        return;
      }

      setState(() {
        _subscription = updated ?? created;
      });

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
            content: Text(updated?.status == 'Active'
                ? (mode.toLowerCase() == 'demo'
                    ? 'Demo plaćanje potvrđeno.'
                    : 'Stripe plaćanje potvrđeno.')
                : 'Plaćanje je poslano. Status možete osvježiti nakon potvrde servera.')),
      );
    } catch (error) {
      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Plaćanje nije završeno: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _isBusy = false);
      }
    }
  }

  Future<void> _cancelSubscription() async {
    final subscription = _subscription;
    if (subscription == null) {
      return;
    }

    final shouldCancel = await showDialog<bool>(
          context: context,
          builder: (context) {
            return AlertDialog(
              title: const Text('Cancel subscription?'),
              content: const Text(
                'Ova akcija prekida pretplatu. Potvrdite da želite nastaviti.',
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.of(context).pop(false),
                  child: const Text('Keep it'),
                ),
                ElevatedButton(
                  onPressed: () => Navigator.of(context).pop(true),
                  child: const Text('Cancel plan'),
                ),
              ],
            );
          },
        ) ??
        false;

    if (!shouldCancel || !mounted) {
      return;
    }

    setState(() => _isBusy = true);
    try {
      final cancelled = await _repository.cancelSubscription(subscription.id);
      if (!mounted) {
        return;
      }

      setState(() {
        _subscription = cancelled;
      });

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Subscription cancelled.')),
      );
    } catch (error) {
      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Cancellation failed: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _isBusy = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final mentor = widget.mentor;
    final subscription = _subscription;
    final titleMentorName =
        mentor?.name ?? subscription?.mentorName ?? 'No mentor selected';

    return Scaffold(
      appBar: AppBar(title: const Text('Subscription')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 8, 20, 24),
        children: [
          if (_isLoading)
            const Center(
              child: Padding(
                padding: EdgeInsets.all(32),
                child: CircularProgressIndicator(),
              ),
            )
          else if (_errorMessage != null)
            AppPanel(
              color: AppTheme.surfaceColor,
              child: Text(
                _errorMessage!,
                style: const TextStyle(color: Colors.redAccent),
              ),
            )
          else ...[
            AppPanel(
              gradient: LinearGradient(
                colors: [
                  AppTheme.accentColor.withValues(alpha: 0.26),
                  AppTheme.surfaceColor,
                ],
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                    decoration: BoxDecoration(
                      color: AppTheme.secondaryColor.withValues(alpha: 0.14),
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: Text(
                      subscription?.status ??
                          (_isOnboardingFlow
                              ? 'Ready to activate'
                              : 'Inactive'),
                      style: Theme.of(context).textTheme.labelLarge?.copyWith(
                            color: AppTheme.secondaryColor,
                          ),
                    ),
                  ),
                  const SizedBox(height: 16),
                  Text(
                    subscription?.planName ??
                        '${mentor?.category ?? 'Coaching'} / 4 Weeks',
                    style: Theme.of(context).textTheme.headlineMedium,
                  ),
                  const SizedBox(height: 10),
                  Text(
                    'Assigned mentor: $titleMentorName',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 10),
                  Text(
                    subscription == null
                        ? 'Questionnaire is ready. Confirm payment to activate the coaching relationship.'
                        : '${subscription.paymentStatus} | ${subscription.renewalLabel} | ${subscription.checkInDay}',
                  ),
                  const SizedBox(height: 18),
                  Wrap(
                    spacing: 10,
                    runSpacing: 10,
                    children: [
                      _SubscriptionTag(
                          label: subscription != null
                              ? subscription.paymentStatus
                              : 'Awaiting payment'),
                      _SubscriptionTag(
                          label: widget.questionnaireAnswers == null
                              ? 'Questionnaire pending'
                              : 'Questionnaire received'),
                      _SubscriptionTag(
                          label: subscription?.hasPublishedPlan == true
                              ? 'Plan active'
                              : 'Plan pending'),
                    ],
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),
            const SectionHeader(
              title: 'Included',
              subtitle:
                  'The client gets clear plan structure, mentor review and visible progress checkpoints.',
            ),
            const SizedBox(height: 12),
            const AppPanel(
              color: AppTheme.surfaceColor,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _ChecklistItem(
                      label: 'Weekly mentor check-in with plan adjustment'),
                  SizedBox(height: 12),
                  _ChecklistItem(
                      label: 'Session structure with recovery-aware pacing'),
                  SizedBox(height: 12),
                  _ChecklistItem(label: 'Progress review and adherence notes'),
                ],
              ),
            ),
            const SizedBox(height: 24),
            const SectionHeader(
              title: 'Status timeline',
              subtitle:
                  'Subscription and payment state are now backed by the API.',
            ),
            const SizedBox(height: 12),
            _TimelineTile(
              title: subscription == null
                  ? 'Questionnaire ready'
                  : 'Payment processed',
              subtitle: subscription == null
                  ? 'Confirm payment to create the active subscription.'
                  : subscription.paymentStatus,
            ),
            const SizedBox(height: 10),
            _TimelineTile(
              title: 'Questionnaire reviewed',
              subtitle: widget.questionnaireAnswers == null
                  ? 'Complete the questionnaire first.'
                  : widget.questionnaireAnswers!['primaryGoal'] ?? '',
            ),
            const SizedBox(height: 10),
            _TimelineTile(
              title: 'Plan availability',
              subtitle: subscription?.hasPublishedPlan == true
                  ? 'A published training plan is already attached to this subscription.'
                  : 'The mentor will publish the first plan after onboarding review.',
            ),
            const SizedBox(height: 24),
            if (_isOnboardingFlow && subscription?.status != 'Active') ...[
              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  onPressed: _isBusy || _paymentMode.isEmpty
                      ? null
                      : _startSubscription,
                  child: _isBusy
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : Text(_paymentMode.toLowerCase() == 'demo'
                          ? 'Potvrdi demo plaćanje'
                          : 'Plati putem Stripe'),
                ),
              ),
              if (subscription != null)
                TextButton(
                    onPressed: _loadCurrentSubscription,
                    child: const Text('Osvježi status')),
            ] else if (subscription != null) ...[
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton(
                      onPressed: _isBusy ? null : _loadCurrentSubscription,
                      child: const Text('Refresh status'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: ElevatedButton(
                      onPressed: _isBusy ? null : _cancelSubscription,
                      child: const Text('Cancel subscription'),
                    ),
                  ),
                ],
              ),
            ] else
              const Text(
                  'Start from the questionnaire to create a subscription request.'),
          ],
        ],
      ),
    );
  }
}

class _SubscriptionTag extends StatelessWidget {
  const _SubscriptionTag({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Text(label),
    );
  }
}

class _ChecklistItem extends StatelessWidget {
  const _ChecklistItem({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Padding(
          padding: EdgeInsets.only(top: 2),
          child:
              Icon(Icons.check_circle_rounded, color: AppTheme.secondaryColor),
        ),
        const SizedBox(width: 12),
        Expanded(child: Text(label)),
      ],
    );
  }
}

class _TimelineTile extends StatelessWidget {
  const _TimelineTile({
    required this.title,
    required this.subtitle,
  });

  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    return AppPanel(
      color: AppTheme.surfaceColor,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            margin: const EdgeInsets.only(top: 2),
            width: 12,
            height: 12,
            decoration: const BoxDecoration(
              color: AppTheme.accentColor,
              shape: BoxShape.circle,
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 6),
                Text(
                  subtitle,
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: AppTheme.textMutedColor,
                      ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
