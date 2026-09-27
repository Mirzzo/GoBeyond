import 'package:flutter/material.dart';

import '../../../../core/services/mentor_service.dart';
import '../../../../core/theme/app_theme.dart';
import '../../../../core/utils/api_error.dart';
import '../../../widgets/dialogs.dart';
import '../../../widgets/panel.dart';
import 'client_description_dialog.dart';
import 'plan_day_dialog.dart';

const _dayNames = {
  1: 'PONEDJELJAK',
  2: 'UTORAK',
  3: 'SRIJEDA',
  4: 'ČETVRTAK',
  5: 'PETAK',
  6: 'SUBOTA',
  7: 'NEDJELJA',
};

/// Mockup 04 — IZRADA TRENING PLANA ZA: `<IME PREZIME>`. Opens as a modal
/// dialog over a dark overlay with a 7-column PON-NED grid, a PREGLED
/// shortcut to the client description, and a bottom OBJAVI PLAN action.
///
/// Exactly one of [planId] / [subscriptionId] should be provided:
/// - [subscriptionId] — "IZRADI PLAN"/"NASTAVI PLAN" from a collaboration
///   request: loads the existing draft for that subscription or creates one
///   (the backend auto-accepts an AwaitingMentor subscription on create).
/// - [planId] — "UREDI PLAN" from Izrađeni planovi: loads that plan directly.
///
/// Returns `true` if anything changed, so the caller can refresh its list.
Future<bool?> showPlanBuilderDialog(
  BuildContext context, {
  int? planId,
  int? subscriptionId,
  required String clientFullName,
}) {
  assert(planId != null || subscriptionId != null);
  return showDialog<bool>(
    context: context,
    barrierDismissible: false,
    builder: (_) => _PlanBuilderDialog(planId: planId, subscriptionId: subscriptionId, clientFullName: clientFullName),
  );
}

class _PlanBuilderDialog extends StatefulWidget {
  const _PlanBuilderDialog({this.planId, this.subscriptionId, required this.clientFullName});

  final int? planId;
  final int? subscriptionId;
  final String clientFullName;

  @override
  State<_PlanBuilderDialog> createState() => _PlanBuilderDialogState();
}

class _PlanBuilderDialogState extends State<_PlanBuilderDialog> {
  final _service = MentorService();
  final _quoteController = TextEditingController();
  bool _loading = true;
  bool _busy = false;
  String? _error;
  Map<String, dynamic>? _plan;
  bool _changed = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _quoteController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      Map<String, dynamic>? plan;
      if (widget.planId != null) {
        plan = await _service.getPlan(widget.planId!);
      } else {
        plan = await _service.getPlanBySubscription(widget.subscriptionId!);
        plan ??= await _service.createPlan(widget.subscriptionId!);
      }
      if (!mounted) return;
      setState(() {
        _plan = plan;
        _quoteController.text = plan?['motivationalQuote'] as String? ?? '';
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = ApiError.from(error).message;
      });
    }
  }

  Map<String, dynamic>? _dayData(int dayOfWeek) {
    final days = (_plan?['days'] as List<dynamic>? ?? const []).map((e) => Map<String, dynamic>.from(e as Map));
    for (final day in days) {
      if (day['dayOfWeek'] == dayOfWeek) return day;
    }
    return null;
  }

  int get _filledDayCount => (_plan?['days'] as List<dynamic>? ?? const []).length;
  bool get _canEdit => _plan?['canEdit'] != false;
  String get _status => _plan?['status'] as String? ?? 'Draft';

  Future<void> _openDay(int dayOfWeek) async {
    final planId = _plan?['id'] as int?;
    if (planId == null) return;
    final updated = await showPlanDayDialog(
      context,
      planId: planId,
      dayOfWeek: dayOfWeek,
      dayName: _dayNames[dayOfWeek]!,
      existingDay: _dayData(dayOfWeek),
      allowDelete: _status == 'Draft',
    );
    if (updated != null && mounted) {
      setState(() {
        _plan = updated;
        _changed = true;
      });
    }
  }

  Future<void> _openClientDescription() async {
    final subscriptionId = _plan?['subscriptionId'] as int?;
    if (subscriptionId == null) return;
    try {
      Map<String, dynamic> description;
      try {
        description = await _service.getCollaborationRequestDetail(subscriptionId);
      } catch (_) {
        description = await _service.getSubscriberDetail(subscriptionId);
      }
      if (!mounted) return;
      await showClientDescriptionDialog(context, description);
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _saveQuote() async {
    final planId = _plan?['id'] as int?;
    if (planId == null) return;
    try {
      final updated = await _service.updatePlan(planId, motivationalQuote: _quoteController.text.trim());
      if (!mounted) return;
      setState(() {
        _plan = updated;
        _changed = true;
      });
      showSuccessSnack(context, 'Motivaciona poruka je sačuvana.');
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _publish() async {
    final planId = _plan?['id'] as int?;
    if (planId == null) return;
    final confirmed = await showConfirmDialog(
      context,
      title: 'Objavi plan',
      message: 'Klijent (${widget.clientFullName}) će odmah biti obaviješten da je plan spreman. Da li želite objaviti plan?',
      confirmLabel: 'OBJAVI PLAN',
    );
    if (!confirmed) return;
    setState(() => _busy = true);
    try {
      final updated = await _service.publishPlan(planId);
      if (!mounted) return;
      setState(() {
        _plan = updated;
        _changed = true;
      });
      showSuccessSnack(context, 'Plan za ${widget.clientFullName} je objavljen.');
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Dialog(
      backgroundColor: AppColors.panel,
      insetPadding: const EdgeInsets.all(32),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 980, maxHeight: 720),
        child: _loading
            ? const Center(child: CircularProgressIndicator())
            : _error != null
                ? Padding(padding: const EdgeInsets.all(32), child: Center(child: Text(_error!, style: const TextStyle(color: AppColors.danger))))
                : _buildContent(),
      ),
    );
  }

  Widget _buildContent() {
    final missing = 7 - _filledDayCount;
    final showPublish = _status != 'Published';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Container(
          padding: const EdgeInsets.fromLTRB(24, 16, 12, 16),
          decoration: const BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.vertical(top: Radius.circular(20))),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  'IZRADA TRENING PLANA ZA: ${widget.clientFullName.toUpperCase()}',
                  style: const TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 17),
                ),
              ),
              PillButton(label: 'PREGLED...', onPressed: _openClientDescription),
              IconButton(
                tooltip: 'Zatvori',
                icon: const Icon(Icons.close, color: Colors.white),
                onPressed: () => Navigator.of(context).pop(_changed),
              ),
            ],
          ),
        ),
        Expanded(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (!_canEdit)
                  Container(
                    margin: const EdgeInsets.only(bottom: 16),
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(color: AppColors.danger.withValues(alpha: 0.15), borderRadius: BorderRadius.circular(10)),
                    child: const Text(
                      'Pretplata više nije aktivna — plan se može samo pregledati, ne i uređivati.',
                      style: TextStyle(color: AppColors.danger),
                    ),
                  ),
                Row(children: [
                  Expanded(
                    child: TextField(
                      controller: _quoteController,
                      enabled: _canEdit,
                      maxLength: 300,
                      style: const TextStyle(color: Colors.white),
                      decoration: const InputDecoration(labelText: 'Motivaciona poruka (opciono)'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  if (_canEdit) ElevatedButton(onPressed: _saveQuote, child: const Text('SAČUVAJ PORUKU')),
                ]),
                const SizedBox(height: 20),
                LayoutBuilder(
                  builder: (context, constraints) {
                    final columnWidth = (constraints.maxWidth - 6 * 8) / 7;
                    return Row(
                      children: List.generate(7, (i) {
                        final dayOfWeek = i + 1;
                        final dayData = _dayData(dayOfWeek);
                        final filled = dayData != null;
                        return Padding(
                          padding: EdgeInsets.only(right: dayOfWeek == 7 ? 0 : 8),
                          child: SizedBox(
                            width: columnWidth,
                            child: Column(
                              children: [
                                Container(
                                  width: double.infinity,
                                  padding: const EdgeInsets.symmetric(vertical: 10),
                                  decoration: BoxDecoration(border: Border.all(color: Colors.white38), borderRadius: BorderRadius.circular(6)),
                                  child: Text(
                                    _dayNames[dayOfWeek]!,
                                    textAlign: TextAlign.center,
                                    style: const TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 11),
                                  ),
                                ),
                                const SizedBox(height: 8),
                                SizedBox(
                                  width: double.infinity,
                                  child: ElevatedButton(
                                    onPressed: _canEdit ? () => _openDay(dayOfWeek) : null,
                                    style: ElevatedButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 12)),
                                    child: filled
                                        ? const Row(mainAxisAlignment: MainAxisAlignment.center, children: [
                                            Icon(Icons.check, size: 14, color: Colors.black),
                                            SizedBox(width: 4),
                                            Text('UREDI', style: TextStyle(fontSize: 11)),
                                          ])
                                        : const Text('IZRADI', style: TextStyle(fontSize: 11)),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        );
                      }),
                    );
                  },
                ),
                const SizedBox(height: 28),
                if (showPublish) ...[
                  Tooltip(
                    message: missing > 0 ? 'Popunite preostalih $missing dana prije objave plana.' : 'Objavi plan klijentu',
                    child: SizedBox(
                      width: double.infinity,
                      child: ElevatedButton(
                        onPressed: (_canEdit && missing == 0 && !_busy) ? _publish : null,
                        child: _busy
                            ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.black))
                            : const Text('OBJAVI PLAN'),
                      ),
                    ),
                  ),
                  if (missing > 0)
                    Padding(
                      padding: const EdgeInsets.only(top: 8),
                      child: Text('Plan mora imati popunjenih svih 7 dana prije objave (nedostaje: $missing).',
                          style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
                    ),
                ] else
                  Container(
                    padding: const EdgeInsets.symmetric(vertical: 12),
                    alignment: Alignment.center,
                    decoration: BoxDecoration(color: AppColors.success.withValues(alpha: 0.15), borderRadius: BorderRadius.circular(10)),
                    child: const Text('Plan je objavljen. Izmjene dana se odmah primjenjuju za klijenta.',
                        style: TextStyle(color: AppColors.success, fontWeight: FontWeight.w600)),
                  ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}
