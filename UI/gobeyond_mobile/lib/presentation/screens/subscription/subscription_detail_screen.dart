import 'package:flutter/material.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/subscription.dart';
import '../../../data/repositories/subscription_repository.dart';
import '../../widgets/app_modal_page.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/state_views.dart';

/// Historija aktivnosti detail: fetches `GET /api/subscriptions/my/{id}` for
/// the full record (the list item from `GET /api/subscriptions/my` alone has
/// no questionnaire/payments) and shows the questionnaire answers and
/// payment history for one past/current subscription.
class SubscriptionDetailScreen extends StatefulWidget {
  const SubscriptionDetailScreen({
    super.key,
    required this.subscriptionId,
    this.mentorFullName,
    this.subscriptionRepository,
  });

  final int subscriptionId;

  /// Shown as the modal title immediately, before the detail call resolves.
  final String? mentorFullName;
  final SubscriptionRepository? subscriptionRepository;

  @override
  State<SubscriptionDetailScreen> createState() =>
      _SubscriptionDetailScreenState();
}

class _SubscriptionDetailScreenState extends State<SubscriptionDetailScreen> {
  late final SubscriptionRepository _repository =
      widget.subscriptionRepository ?? ApiSubscriptionRepository();

  late Future<Subscription> _future;

  @override
  void initState() {
    super.initState();
    _future = _repository.getSubscriptionDetail(widget.subscriptionId);
  }

  void _reload() {
    setState(() {
      _future = _repository.getSubscriptionDetail(widget.subscriptionId);
    });
  }

  @override
  Widget build(BuildContext context) {
    return AppModalPage(
      title: widget.mentorFullName ?? 'Detalji pretplate',
      body: FutureBuilder<Subscription>(
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
          return _SubscriptionDetailBody(subscription: snapshot.data!);
        },
      ),
    );
  }
}

class _SubscriptionDetailBody extends StatelessWidget {
  const _SubscriptionDetailBody({required this.subscription});

  final Subscription subscription;

  @override
  Widget build(BuildContext context) {
    final questionnaire = subscription.questionnaire;

    return ListView(
      children: [
        AppPanel(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _Row('Mentor', subscription.mentorFullName),
              _Row('Vrsta treninga', subscription.trainingTypeName),
              _Row('Status', subscriptionStatusLabel(subscription.status)),
              _Row('Cijena',
                  Formatters.price(subscription.price, subscription.currency)),
              _Row('Datum kreiranja',
                  Formatters.dateTimeLabel(subscription.createdAt)),
              if (subscription.startDate != null)
                _Row('Datum početka',
                    Formatters.dateTimeLabel(subscription.startDate)),
              if (subscription.endDate != null)
                _Row('Datum isteka',
                    Formatters.dateTimeLabel(subscription.endDate)),
              if (subscription.statusReason != null &&
                  subscription.statusReason!.isNotEmpty)
                _Row('Napomena', subscription.statusReason!),
            ],
          ),
        ),
        if (questionnaire != null) ...[
          const SizedBox(height: 18),
          const Text('ODGOVORI NA UPITNIK',
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 14)),
          const SizedBox(height: 10),
          AppPanel(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _QA('Glavni cilj', questionnaire.primaryGoal),
                _QA('Vrijeme za ostvarivanje cilja',
                    questionnaire.timeCommitment),
                _QA('Zdravstveni problemi', questionnaire.healthIssues),
                _QA('Terapija/lijekovi', questionnaire.medications),
                _QA('Sedmični treninzi', questionnaire.weeklySessions),
                _QA('Aktivnost van treninga', questionnaire.outsideActivity,
                    isLast: true),
              ],
            ),
          ),
        ],
        const SizedBox(height: 18),
        const Text('PLAĆANJA',
            style: TextStyle(fontWeight: FontWeight.w800, fontSize: 14)),
        const SizedBox(height: 10),
        if (subscription.payments.isEmpty)
          const Text('Nema evidentiranih plaćanja.',
              style: TextStyle(color: AppTheme.textMuted))
        else
          for (final payment in subscription.payments)
            Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: AppPanel(
                padding:
                    const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                child: Row(
                  children: [
                    Icon(
                      _paymentStatusIcon(payment.status),
                      color: _paymentStatusColor(payment.status),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            '${payment.purpose == 'Renewal' ? 'Produženje' : 'Početna uplata'} · ${Formatters.price(payment.amount, payment.currency)}',
                            style: const TextStyle(fontWeight: FontWeight.w700),
                          ),
                          Text(
                            '${_paymentStatusLabel(payment.status)} · ${Formatters.dateTimeLabel(payment.paidAt ?? payment.createdAt)}',
                            style: const TextStyle(
                                color: AppTheme.textMuted, fontSize: 12),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
      ],
    );
  }

  // `Payment.status` (api-contract.md §1 + changelog): Pending, Succeeded,
  // Failed, Refunded, RefundPending, Disputed (the charge was disputed at
  // the client's bank, so it is not refunded automatically). Any other
  // value is a future/unrecognized status the backend may add later - shown
  // as-is rather than mislabeled "Na čekanju" (contract: "UI mora podržati
  // nepoznate/nove vrijednosti").
  String _paymentStatusLabel(String status) {
    switch (status) {
      case 'Pending':
        return 'Na čekanju';
      case 'Succeeded':
        return 'Uspješno';
      case 'Failed':
        return 'Neuspješno';
      case 'Refunded':
        return 'Refundirano';
      case 'RefundPending':
        return 'Povrat novca u obradi';
      case 'Disputed':
        return 'Osporeno';
      default:
        return status;
    }
  }

  IconData _paymentStatusIcon(String status) {
    switch (status) {
      case 'Succeeded':
        return Icons.check_circle_rounded;
      case 'Failed':
        return Icons.cancel_rounded;
      case 'Refunded':
      case 'RefundPending':
        return Icons.undo_rounded;
      case 'Disputed':
        return Icons.gavel_rounded;
      default:
        return Icons.hourglass_top_rounded;
    }
  }

  Color _paymentStatusColor(String status) {
    switch (status) {
      case 'Succeeded':
        return AppTheme.success;
      case 'Failed':
        return AppTheme.danger;
      case 'Disputed':
        return AppTheme.accent;
      default:
        return AppTheme.textMuted;
    }
  }
}

class _Row extends StatelessWidget {
  const _Row(this.label, this.value);

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 130,
            child:
                Text(label, style: const TextStyle(color: AppTheme.textMuted)),
          ),
          Expanded(
              child: Text(value,
                  style: const TextStyle(fontWeight: FontWeight.w600))),
        ],
      ),
    );
  }
}

class _QA extends StatelessWidget {
  const _QA(this.question, this.answer, {this.isLast = false});

  final String question;
  final String answer;
  final bool isLast;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(bottom: isLast ? 0 : 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(question,
              style:
                  const TextStyle(fontWeight: FontWeight.w700, fontSize: 12.5)),
          const SizedBox(height: 3),
          Text(answer, style: const TextStyle(color: AppTheme.textMuted)),
        ],
      ),
    );
  }
}
