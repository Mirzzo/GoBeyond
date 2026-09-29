import 'package:flutter/material.dart';
import 'package:flutter_stripe/flutter_stripe.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/payment_sheet_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/utils/stripe_error_messages.dart';
import '../../../core/utils/validators.dart';
import '../../../data/models/review.dart';
import '../../../data/models/subscription.dart';
import '../../../data/repositories/mentor_repository.dart';
import '../../../data/repositories/payment_repository.dart';
import '../../../data/repositories/review_repository.dart';
import '../../../data/repositories/subscription_repository.dart';
import '../../widgets/app_dialogs.dart';
import '../../widgets/app_network_image.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/gb_scaffold.dart';
import '../../widgets/primary_button.dart';
import '../../widgets/star_rating.dart';
import '../../widgets/state_views.dart';
import '../messages/message_chat_screen.dart';
import 'subscription_detail_screen.dart';

/// Priority order for picking which subscription is "current" when a client
/// has more than one in a blocking-like status: a live Active saradnja
/// outranks one still awaiting the mentor, which outranks one still awaiting
/// payment.
const _currentSubscriptionPriority = [
  'Active',
  'AwaitingMentor',
  'PendingPayment'
];

/// Thread `canSend`/messaging is only meaningful once the mentor has at
/// least seen the request (AwaitingMentor) or accepted it (Active) — a
/// PendingPayment subscription has no message thread yet on the backend.
const _messageableStatuses = {'AwaitingMentor', 'Active'};

/// Pretplata: current subscription card with actions (PRODUŽI/OTKAŽI/PORUKA
/// MENTORU/RECENZIJA) plus the full subscription history.
class SubscriptionScreen extends StatefulWidget {
  const SubscriptionScreen({
    super.key,
    this.subscriptionRepository,
    this.paymentRepository,
    this.reviewRepository,
    this.mentorRepository,
  });

  final SubscriptionRepository? subscriptionRepository;
  final PaymentRepository? paymentRepository;
  final ReviewRepository? reviewRepository;
  final MentorRepository? mentorRepository;

  @override
  State<SubscriptionScreen> createState() => _SubscriptionScreenState();
}

class _SubscriptionScreenState extends State<SubscriptionScreen> {
  late final SubscriptionRepository _subscriptionRepository =
      widget.subscriptionRepository ?? ApiSubscriptionRepository();
  late final PaymentRepository _paymentRepository =
      widget.paymentRepository ?? ApiPaymentRepository();
  late final ReviewRepository _reviewRepository =
      widget.reviewRepository ?? ApiReviewRepository();
  late final MentorRepository _mentorRepository =
      widget.mentorRepository ?? ApiMentorRepository();

  late Future<List<Subscription>> _future;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _future = _subscriptionRepository.getMySubscriptions();
  }

  void _reload() => setState(() {
        _future = _subscriptionRepository.getMySubscriptions();
      });

  /// Drives the Stripe PaymentSheet for both "NASTAVI PLAĆANJE" (a lingering
  /// PendingPayment subscription) and "PRODUŽI" (renewing an Active one) —
  /// `create-intent` picks the right purpose (Initial/Renewal) server-side
  /// from the subscription's current status.
  Future<void> _startPayment(Subscription subscription) async {
    setState(() => _busy = true);
    try {
      final intent = await _paymentRepository.createIntent(subscription.id);
      Stripe.publishableKey = intent.publishableKey;
      await Stripe.instance.applySettings();
      await Stripe.instance.initPaymentSheet(
        paymentSheetParameters: SetupPaymentSheetParameters(
          paymentIntentClientSecret: intent.clientSecret,
          merchantDisplayName: 'GoBeyond',
          appearance: paymentSheetAppearance,
        ),
      );
      await Stripe.instance.presentPaymentSheet();
      // Only reachable once the sheet reports success, so the backend must
      // always learn about it - otherwise the subscription stays stuck in
      // PendingPayment/Active-but-not-renewed even though the card charged.
      await _paymentRepository.confirmPayment(intent.paymentId);
      if (!mounted) return;
      showSuccessSnackBar(
        context,
        subscription.status == 'PendingPayment'
            ? 'Plaćanje je uspješno. Mentor je dobio vaš zahtjev za saradnju.'
            : 'Pretplata je uspješno produžena za narednih 30 dana.',
      );
      _reload();
    } on StripeException catch (error) {
      if (!mounted) return;
      showErrorSnackBar(context, stripePaymentErrorMessage(error));
    } catch (error) {
      if (!mounted) return;
      final apiError = ApiException.from(error);
      showErrorSnackBar(context, apiError.message);
      if (apiError.statusCode == 409) {
        // create-intent's 409s (`api-contract.md` v1.2) mean the backend
        // already moved this subscription/payment on without us - either a
        // previous attempt succeeded after we lost the response (payment
        // applied, subscription now Active/AwaitingMentor) or a duplicate
        // attempt is still processing. Refresh so the card's status/buttons
        // reflect reality instead of leaving the user staring at a stale
        // "NASTAVI PLAĆANJE"/"PRODUŽI" button that looks like it failed.
        _reload();
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _cancel(Subscription subscription) async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Otkaži pretplatu',
      message:
          'Da li ste sigurni da želite otkazati pretplatu kod mentora ${subscription.mentorFullName}? '
          'Uplaćeni iznos se ne vraća (nema povrata novca).',
      confirmLabel: 'OTKAŽI',
      destructive: true,
    );
    if (!confirmed) return;

    setState(() => _busy = true);
    try {
      await _subscriptionRepository.cancelSubscription(subscription.id);
      if (!mounted) return;
      showSuccessSnackBar(context, 'Pretplata je otkazana.');
      _reload();
    } catch (error) {
      if (!mounted) return;
      showErrorSnackBar(context, ApiException.from(error).message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _writeReview(Subscription subscription) async {
    final result = await showDialog<_ReviewResult>(
      context: context,
      builder: (_) => const _ReviewDialog(),
    );
    if (result == null) return;

    try {
      await _reviewRepository.createReview(
        subscriptionId: subscription.id,
        rating: result.rating,
        comment: result.comment,
      );
      if (!mounted) return;
      showSuccessSnackBar(context, 'Hvala vam! Vaša recenzija je objavljena.');
      _reload();
    } catch (error) {
      if (!mounted) return;
      showErrorSnackBar(context, ApiException.from(error).message);
    }
  }

  Future<void> _editReview(Subscription subscription) async {
    List<Review> reviews;
    try {
      reviews = await _mentorRepository
          .getMentorReviews(subscription.mentorProfileId);
    } catch (error) {
      if (!mounted) return;
      showErrorSnackBar(context, ApiException.from(error).message);
      return;
    }
    final mine = reviews.where((r) => r.id == subscription.reviewId).toList();
    final existing = mine.isNotEmpty ? mine.first : null;
    if (!mounted) return;

    final result = await showDialog<_ReviewResult>(
      context: context,
      builder: (_) => _ReviewDialog(
        initialRating: existing?.rating ?? 5,
        initialComment: existing?.comment ?? '',
      ),
    );
    if (result == null) return;

    try {
      await _reviewRepository.updateReview(
        reviewId: subscription.reviewId!,
        rating: result.rating,
        comment: result.comment,
      );
      if (!mounted) return;
      showSuccessSnackBar(context, 'Recenzija je uspješno ažurirana.');
      _reload();
    } catch (error) {
      if (!mounted) return;
      showErrorSnackBar(context, ApiException.from(error).message);
    }
  }

  Future<void> _deleteReview(Subscription subscription) async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Obriši recenziju',
      message: 'Da li ste sigurni da želite obrisati svoju recenziju?',
      confirmLabel: 'Obriši',
      destructive: true,
    );
    if (!confirmed || subscription.reviewId == null) return;

    try {
      await _reviewRepository.deleteReview(subscription.reviewId!);
      if (!mounted) return;
      showSuccessSnackBar(context, 'Recenzija je obrisana.');
      _reload();
    } catch (error) {
      if (!mounted) return;
      showErrorSnackBar(context, ApiException.from(error).message);
    }
  }

  @override
  Widget build(BuildContext context) {
    return GbScaffold(
      body: FutureBuilder<List<Subscription>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const LoadingView();
          }
          if (snapshot.hasError) {
            return ErrorView(
              message: ApiException.from(snapshot.error!).message,
              onRetry: _reload,
            );
          }

          final subscriptions = snapshot.data!;
          if (subscriptions.isEmpty) {
            return const EmptyStateView(
              message:
                  'Nemate nijednu pretplatu. Odaberite mentora na početnoj stranici.',
              icon: Icons.card_membership_rounded,
            );
          }

          Subscription? current;
          for (final status in _currentSubscriptionPriority) {
            final match = subscriptions.where((s) => s.status == status);
            if (match.isNotEmpty) {
              current = match.first;
              break;
            }
          }

          final history = subscriptions.where((s) => s != current).toList();

          return ListView(
            padding: const EdgeInsets.all(20),
            children: [
              const Text('TRENUTNA PRETPLATA',
                  style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
              const SizedBox(height: 12),
              if (current == null)
                const AppPanel(
                  child: Text(
                    'Trenutno nemate aktivnu pretplatu.',
                    style: TextStyle(color: AppTheme.textMuted),
                  ),
                )
              else
                _CurrentSubscriptionCard(
                  subscription: current,
                  busy: _busy,
                  onStartPayment: () => _startPayment(current!),
                  onCancel: () => _cancel(current!),
                  onMessage: () => Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => MessageChatScreen(
                        subscriptionId: current!.id,
                        otherPartyName: current.mentorFullName,
                        otherPartyPhotoUrl: current.mentorPhotoUrl,
                      ),
                    ),
                  ),
                  onWriteReview: () => _writeReview(current!),
                  onEditReview: () => _editReview(current!),
                  onDeleteReview: () => _deleteReview(current!),
                ),
              const SizedBox(height: 28),
              const Text('HISTORIJA AKTIVNOSTI',
                  style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
              const SizedBox(height: 12),
              if (history.isEmpty)
                const Text('Nema prethodnih pretplata.',
                    style: TextStyle(color: AppTheme.textMuted))
              else
                for (final subscription in history)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 12),
                    child: AppPanel(
                      onTap: () => Navigator.of(context).push(
                        MaterialPageRoute(
                          builder: (_) => SubscriptionDetailScreen(
                            subscriptionId: subscription.id,
                            mentorFullName: subscription.mentorFullName,
                          ),
                        ),
                      ),
                      child: Row(
                        children: [
                          AppNetworkImage(
                            url: subscription.mentorPhotoUrl,
                            width: 48,
                            height: 48,
                            borderRadius: 24,
                          ),
                          const SizedBox(width: 14),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(subscription.mentorFullName,
                                    style: const TextStyle(
                                        fontWeight: FontWeight.w700)),
                                const SizedBox(height: 3),
                                Text(
                                    subscriptionStatusLabel(
                                        subscription.status),
                                    style: const TextStyle(
                                        color: AppTheme.textMuted,
                                        fontSize: 12.5)),
                              ],
                            ),
                          ),
                          const Icon(Icons.chevron_right_rounded,
                              color: AppTheme.textMuted),
                        ],
                      ),
                    ),
                  ),
            ],
          );
        },
      ),
    );
  }
}

class _CurrentSubscriptionCard extends StatelessWidget {
  const _CurrentSubscriptionCard({
    required this.subscription,
    required this.busy,
    required this.onStartPayment,
    required this.onCancel,
    required this.onMessage,
    required this.onWriteReview,
    required this.onEditReview,
    required this.onDeleteReview,
  });

  final Subscription subscription;
  final bool busy;
  final VoidCallback onStartPayment;
  final VoidCallback onCancel;
  final VoidCallback onMessage;
  final VoidCallback onWriteReview;
  final VoidCallback onEditReview;
  final VoidCallback onDeleteReview;

  @override
  Widget build(BuildContext context) {
    return AppPanel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              AppNetworkImage(
                url: subscription.mentorPhotoUrl,
                width: 64,
                height: 64,
                borderRadius: 32,
                yellowBorder: true,
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(subscription.mentorFullName,
                        style: const TextStyle(
                            fontWeight: FontWeight.w800, fontSize: 16)),
                    Text(subscription.trainingTypeName,
                        style: const TextStyle(color: AppTheme.textMuted)),
                  ],
                ),
              ),
              Chip(label: Text(subscriptionStatusLabel(subscription.status))),
            ],
          ),
          const SizedBox(height: 14),
          if (subscription.startDate != null)
            _InfoLine(
                'Početak', Formatters.dateTimeLabel(subscription.startDate)),
          if (subscription.endDate != null)
            _InfoLine('Kraj', Formatters.dateTimeLabel(subscription.endDate)),
          _InfoLine('Cijena',
              Formatters.price(subscription.price, subscription.currency)),
          const SizedBox(height: 16),
          Wrap(
            spacing: 10,
            runSpacing: 10,
            children: [
              if (subscription.status == 'PendingPayment')
                SizedBox(
                  width: 190,
                  child: PrimaryButton(
                    label: 'NASTAVI PLAĆANJE',
                    height: 46,
                    isLoading: busy,
                    onPressed: onStartPayment,
                  ),
                )
              else if (subscription.canRenew)
                SizedBox(
                  width: 150,
                  child: PrimaryButton(
                      label: 'PRODUŽI',
                      height: 46,
                      isLoading: busy,
                      onPressed: onStartPayment),
                ),
              if (subscription.canCancel)
                SizedBox(
                  width: 150,
                  child: OutlinedButton(
                    onPressed: busy ? null : onCancel,
                    child: const Text('OTKAŽI'),
                  ),
                ),
              // A message thread only exists once the mentor has seen the
              // request (AwaitingMentor) or accepted it (Active); a
              // PendingPayment subscription has no thread yet on the backend.
              if (_messageableStatuses.contains(subscription.status))
                SizedBox(
                  width: 190,
                  child: OutlinedButton.icon(
                    onPressed: onMessage,
                    icon:
                        const Icon(Icons.chat_bubble_outline_rounded, size: 18),
                    label: const Text('PORUKA MENTORU'),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 14),
          if (subscription.canReview)
            SizedBox(
              width: double.infinity,
              child: OutlinedButton.icon(
                onPressed: onWriteReview,
                icon: const Icon(Icons.star_border_rounded),
                label: const Text('NAPIŠI RECENZIJU'),
              ),
            )
          else if (subscription.reviewId != null)
            Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: onEditReview,
                    icon: const Icon(Icons.edit_rounded, size: 18),
                    label: const Text('UREDI RECENZIJU'),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: onDeleteReview,
                    icon: const Icon(Icons.delete_outline_rounded,
                        size: 18, color: AppTheme.danger),
                    label: const Text('OBRIŠI',
                        style: TextStyle(color: AppTheme.danger)),
                  ),
                ),
              ],
            ),
        ],
      ),
    );
  }
}

class _InfoLine extends StatelessWidget {
  const _InfoLine(this.label, this.value);

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: [
          Text('$label: ', style: const TextStyle(color: AppTheme.textMuted)),
          Expanded(
            child: Text(
              value,
              style: const TextStyle(fontWeight: FontWeight.w700),
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ],
      ),
    );
  }
}

class _ReviewResult {
  const _ReviewResult(this.rating, this.comment);

  final int rating;
  final String comment;
}

class _ReviewDialog extends StatefulWidget {
  const _ReviewDialog({this.initialRating = 5, this.initialComment = ''});

  final int initialRating;
  final String initialComment;

  @override
  State<_ReviewDialog> createState() => _ReviewDialogState();
}

class _ReviewDialogState extends State<_ReviewDialog> {
  final _formKey = GlobalKey<FormState>();
  late int _rating = widget.initialRating;
  late final _commentController =
      TextEditingController(text: widget.initialComment);

  @override
  void dispose() {
    _commentController.dispose();
    super.dispose();
  }

  void _submit() {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    Navigator.of(context)
        .pop(_ReviewResult(_rating, _commentController.text.trim()));
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Row(
        children: [
          const Expanded(child: Text('Recenzija')),
          IconButton(
            icon: const Icon(Icons.close_rounded),
            onPressed: () => Navigator.of(context).pop(),
          ),
        ],
      ),
      content: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Center(
              child: StarRatingInput(
                rating: _rating,
                onChanged: (value) => setState(() => _rating = value),
              ),
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _commentController,
              maxLines: 4,
              maxLength: 1000,
              decoration: const InputDecoration(labelText: 'Komentar'),
              validator: (v) => Validators.textLength(v,
                  min: 10, max: 1000, label: 'Komentar'),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('Odustani')),
        FilledButton(onPressed: _submit, child: const Text('SAČUVAJ')),
      ],
    );
  }
}
