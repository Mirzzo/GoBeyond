import 'package:flutter/material.dart';

import '../../../core/services/mentor_service.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/api_error.dart';
import '../../widgets/dialogs.dart';
import '../../widgets/panel.dart';
import 'widgets/client_description_dialog.dart';
import 'widgets/plan_builder_dialog.dart';

/// Warning shown in the ODBIJ dialog (api-contract.md §6): the client's
/// payment is refunded, unless the charge is disputed at the client's bank
/// (Stripe then refuses the refund and the reject response says so).
String collaborationRejectWarning(String clientFullName) =>
    'Klijentu $clientFullName će biti vraćen novac za uplatu (refundacija), osim ako je naplata osporena kod banke klijenta.';

/// Mockup 03 — ZAHTJEVI ZA SURADNJU.
class MentorCollaborationRequestsScreen extends StatefulWidget {
  const MentorCollaborationRequestsScreen({super.key});

  @override
  State<MentorCollaborationRequestsScreen> createState() => _MentorCollaborationRequestsScreenState();
}

class _MentorCollaborationRequestsScreenState extends State<MentorCollaborationRequestsScreen> {
  final _service = MentorService();
  final _searchController = TextEditingController();
  bool _loading = true;
  List<Map<String, dynamic>> _requests = const [];

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
      final requests = await _service.getCollaborationRequests(search: _searchController.text);
      if (!mounted) return;
      setState(() {
        _requests = requests;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _accept(Map<String, dynamic> request) async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Prihvati zahtjev',
      message: 'Da li prihvatate zahtjev za suradnju sa klijentom ${request['clientFullName']}?',
      confirmLabel: 'PRIHVATI',
    );
    if (!confirmed) return;
    try {
      await _service.acceptCollaborationRequest(request['subscriptionId'] as int);
      if (!mounted) return;
      showSuccessSnack(context, 'Zahtjev klijenta ${request['clientFullName']} je prihvaćen.');
      _load();
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _reject(Map<String, dynamic> request) async {
    final reason = await showReasonDialog(
      context,
      title: 'Odbij zahtjev',
      label: 'Razlog odbijanja (10-500 znakova)',
      warning: collaborationRejectWarning(request['clientFullName'] as String? ?? ''),
      confirmLabel: 'ODBIJ',
    );
    if (reason == null) return;
    try {
      final message = await _service.rejectCollaborationRequest(request['subscriptionId'] as int, reason);
      if (!mounted) return;
      showSuccessSnack(context, message);
      _load();
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _openDetail(Map<String, dynamic> request) async {
    try {
      final detail = await _service.getCollaborationRequestDetail(request['subscriptionId'] as int);
      if (!mounted) return;
      final isAwaiting = request['status'] == 'AwaitingMentor';
      await showClientDescriptionDialog<void>(
        context,
        detail,
        extraActions: isAwaiting
            ? [
                OutlinedButton(
                  style: OutlinedButton.styleFrom(foregroundColor: AppColors.danger, side: const BorderSide(color: AppColors.danger)),
                  onPressed: () {
                    Navigator.of(context).pop();
                    _reject(request);
                  },
                  child: const Text('ODBIJ'),
                ),
                const SizedBox(width: 8),
                ElevatedButton(
                  onPressed: () {
                    Navigator.of(context).pop();
                    _accept(request);
                  },
                  child: const Text('PRIHVATI'),
                ),
              ]
            : null,
      );
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _openPlanBuilder(Map<String, dynamic> request) async {
    final changed = await showPlanBuilderDialog(
      context,
      subscriptionId: request['subscriptionId'] as int,
      clientFullName: request['clientFullName'] as String? ?? '',
    );
    if (changed == true) _load();
  }

  @override
  Widget build(BuildContext context) {
    return ContentPanel(
      title: 'ZAHTJEVI ZA SURADNJU',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SearchField(controller: _searchController, hintText: 'Pretraga po imenu i prezimenu klijenta', onSubmitted: (_) => _load()),
          const SizedBox(height: 16),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _requests.isEmpty
                    ? const EmptyState(message: 'Nema zahtjeva za suradnju.')
                    : ListView.separated(
                        itemCount: _requests.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 10),
                        itemBuilder: (context, index) {
                          final request = _requests[index];
                          final hasDraftPlan = request['planId'] != null && request['planStatus'] != 'Published';
                          return Container(
                            padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 14),
                            decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(14)),
                            child: Row(
                              children: [
                                SizedBox(width: 34, child: Text('${index + 1}.', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold))),
                                Expanded(child: Text(request['clientFullName'] as String? ?? '', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold))),
                                StatusChip(label: SubscriptionStatusPresentation.label(request['status'] as String? ?? ''), color: SubscriptionStatusPresentation.color(request['status'] as String? ?? '')),
                                const SizedBox(width: 12),
                                PillButton(label: 'PREGLED..', onPressed: () => _openDetail(request)),
                                const SizedBox(width: 10),
                                PillButton(
                                  label: hasDraftPlan ? 'NASTAVI PLAN' : 'IZRADI PLAN',
                                  onPressed: () => _openPlanBuilder(request),
                                ),
                              ],
                            ),
                          );
                        },
                      ),
          ),
        ],
      ),
    );
  }
}
