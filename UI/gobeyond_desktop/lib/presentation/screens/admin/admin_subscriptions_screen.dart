import 'package:flutter/material.dart';

import '../../../core/services/admin_service.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/api_error.dart';
import '../../../core/utils/formatters.dart';
import '../../widgets/dialogs.dart';
import '../../widgets/panel.dart';

const _statusOptions = ['PendingPayment', 'AwaitingMentor', 'Active', 'Rejected', 'Cancelled', 'Expired'];

/// Warning shown in the OTKAŽI confirmation dialog, matching what admin
/// cancel does (api-contract.md §5): the client is always notified, the
/// mentor only when the request reached them. A PendingPayment subscription
/// was never paid, so the mentor never saw it and gets no notification. An
/// AwaitingMentor subscription was paid but not yet accepted, so the
/// client's payment is refunded, unless the charge is disputed at the
/// client's bank (Stripe then refuses the refund).
String subscriptionCancelWarning({
  required String? status,
  required String clientFullName,
  required String mentorFullName,
}) {
  if (status == 'PendingPayment') {
    return 'Klijent ($clientFullName) dobija obavijest o otkazivanju. Mentor ne dobija obavijest jer zahtjev nije plaćen.';
  }
  final base = 'Klijent ($clientFullName) i mentor ($mentorFullName) će biti obaviješteni o otkazivanju.';
  if (status == 'AwaitingMentor') {
    return '$base Klijentova uplata će biti vraćena, osim ako je naplata osporena kod banke klijenta.';
  }
  return base;
}

/// Whether any payment of an AdminSubscription is `Disputed` (not refunded,
/// the dispute on Stripe decides the outcome).
bool hasDisputedPayment(Map<String, dynamic> subscription) =>
    (subscription['payments'] as List<dynamic>? ?? const []).any((p) => p is Map && p['status'] == 'Disputed');

/// UPRAVLJANJE PRETPLATAMA.
class AdminSubscriptionsScreen extends StatefulWidget {
  const AdminSubscriptionsScreen({super.key});

  @override
  State<AdminSubscriptionsScreen> createState() => _AdminSubscriptionsScreenState();
}

class _AdminSubscriptionsScreenState extends State<AdminSubscriptionsScreen> {
  final _service = AdminService();
  final _searchController = TextEditingController();
  bool _loading = true;
  List<Map<String, dynamic>> _subscriptions = const [];
  String? _statusFilter;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final subscriptions = await _service.getSubscriptions(search: _searchController.text, status: _statusFilter);
      if (!mounted) return;
      setState(() {
        _subscriptions = subscriptions;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _openDetail(Map<String, dynamic> subscription) async {
    final canCancel = subscription['status'] == 'PendingPayment' ||
        subscription['status'] == 'AwaitingMentor' ||
        subscription['status'] == 'Active';
    await showGbDialog<void>(
      context: context,
      title: '${subscription['clientFullName']} — ${subscription['mentorFullName']}',
      width: 520,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _row('Klijent', subscription['clientFullName'] as String? ?? '-'),
          _row('Mentor', subscription['mentorFullName'] as String? ?? '-'),
          _row('Vrsta treninga', subscription['trainingTypeName'] as String? ?? '-'),
          _row('Status', SubscriptionStatusPresentation.label(subscription['status'] as String? ?? '')),
          _row('Cijena', Formatters.money(subscription['price'] as num?, currency: subscription['currency'] as String? ?? 'usd')),
          _row('Datum kreiranja', Formatters.date(subscription['createdAt'] as String?)),
          _row('Početak', Formatters.date(subscription['startDate'] as String?)),
          _row('Kraj', Formatters.date(subscription['endDate'] as String?)),
          if ((subscription['statusReason'] as String?)?.isNotEmpty == true) _row('Napomena', subscription['statusReason'] as String),
          const SizedBox(height: 12),
          const Text('Uplate', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold)),
          const SizedBox(height: 6),
          ..._paymentRows(subscription['payments']),
        ],
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Zatvori')),
        if (canCancel) ...[
          const SizedBox(width: 8),
          ElevatedButton(
            style: ElevatedButton.styleFrom(backgroundColor: AppColors.danger, foregroundColor: Colors.white),
            onPressed: () {
              Navigator.of(context).pop();
              _cancel(subscription);
            },
            child: const Text('OTKAŽI'),
          ),
        ],
      ],
    );
  }

  List<Widget> _paymentRows(Object? payments) {
    final list = (payments as List<dynamic>? ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList();
    if (list.isEmpty) {
      return const [Text('Nema uplata.', style: TextStyle(color: AppColors.textMuted))];
    }
    return list.map((payment) {
      final status = payment['status'] as String? ?? '';
      final amount = Formatters.money(payment['amount'] as num?, currency: payment['currency'] as String? ?? 'usd');
      final date = Formatters.date((payment['paidAt'] ?? payment['createdAt']) as String?);
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(
          children: [
            Expanded(
              child: Text(
                '${PaymentStatusPresentation.purposeLabel(payment['purpose'] as String? ?? '')} · $amount · $date',
                style: const TextStyle(color: Colors.white),
              ),
            ),
            StatusChip(label: PaymentStatusPresentation.label(status), color: PaymentStatusPresentation.color(status)),
          ],
        ),
      );
    }).toList();
  }

  Widget _row(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(width: 140, child: Text(label, style: const TextStyle(color: AppColors.textMuted))),
          Expanded(child: Text(value, style: const TextStyle(color: Colors.white))),
        ],
      ),
    );
  }

  Future<void> _cancel(Map<String, dynamic> subscription) async {
    final reason = await showReasonDialog(
      context,
      title: 'Otkaži pretplatu',
      label: 'Razlog otkazivanja (5-300 znakova)',
      minLength: 5,
      maxLength: 300,
      warning: subscriptionCancelWarning(
        status: subscription['status'] as String?,
        clientFullName: subscription['clientFullName'] as String? ?? '',
        mentorFullName: subscription['mentorFullName'] as String? ?? '',
      ),
      confirmLabel: 'OTKAŽI',
    );
    if (reason == null) return;
    try {
      final result = await _service.cancelSubscription(subscription['id'] as int, reason);
      if (!mounted) return;
      showSuccessSnack(context, 'Pretplata je otkazana.');
      _load();
      await showPaymentWarningIfAny(context, result['warning']);
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  @override
  Widget build(BuildContext context) {
    return ContentPanel(
      title: 'UPRAVLJANJE PRETPLATAMA',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(children: [
            Expanded(child: SearchField(controller: _searchController, hintText: 'Pretraga po klijentu ili mentoru', onSubmitted: (_) => _load())),
            const SizedBox(width: 12),
            SizedBox(
              width: 220,
              child: DropdownButtonFormField<String?>(
                initialValue: _statusFilter,
                decoration: const InputDecoration(labelText: 'Status'),
                items: [
                  const DropdownMenuItem(value: null, child: Text('Svi statusi')),
                  ..._statusOptions.map((s) => DropdownMenuItem(value: s, child: Text(SubscriptionStatusPresentation.label(s)))),
                ],
                onChanged: (value) {
                  setState(() => _statusFilter = value);
                  _load();
                },
              ),
            ),
          ]),
          const SizedBox(height: 16),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _subscriptions.isEmpty
                    ? const EmptyState(message: 'Nema pretplata koje odgovaraju pretrazi.')
                    : SingleChildScrollView(
                        child: DataTable(
                          columns: const [
                            DataColumn(label: Text('Klijent')),
                            DataColumn(label: Text('Mentor')),
                            DataColumn(label: Text('Vrsta treninga')),
                            DataColumn(label: Text('Status')),
                            DataColumn(label: Text('Cijena')),
                            DataColumn(label: Text('Period')),
                            DataColumn(label: Text('')),
                          ],
                          rows: _subscriptions.map((subscription) {
                            final status = subscription['status'] as String? ?? '';
                            return DataRow(cells: [
                              DataCell(Text(subscription['clientFullName'] as String? ?? '-')),
                              DataCell(Text(subscription['mentorFullName'] as String? ?? '-')),
                              DataCell(Text(subscription['trainingTypeName'] as String? ?? '-')),
                              DataCell(Row(mainAxisSize: MainAxisSize.min, children: [
                                StatusChip(label: SubscriptionStatusPresentation.label(status), color: SubscriptionStatusPresentation.color(status)),
                                if (hasDisputedPayment(subscription)) ...[
                                  const SizedBox(width: 6),
                                  const Tooltip(
                                    message: 'Uplata je osporena kod banke klijenta i nije vraćena.',
                                    child: Icon(Icons.report_problem_outlined, color: AppColors.danger, size: 18),
                                  ),
                                ],
                              ])),
                              DataCell(Text(Formatters.money(subscription['price'] as num?, currency: subscription['currency'] as String? ?? 'usd'))),
                              DataCell(Text('${Formatters.date(subscription['startDate'] as String?)} - ${Formatters.date(subscription['endDate'] as String?)}')),
                              DataCell(PillButton(label: 'DETALJI', dense: true, onPressed: () => _openDetail(subscription))),
                            ]);
                          }).toList(),
                        ),
                      ),
          ),
        ],
      ),
    );
  }
}
