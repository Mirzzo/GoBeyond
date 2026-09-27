import 'package:flutter/material.dart';

import '../../../core/services/mentor_service.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/api_error.dart';
import '../../widgets/dialogs.dart';
import '../../widgets/panel.dart';
import 'widgets/plan_builder_dialog.dart';
import 'widgets/plan_view_dialog.dart';

const _statusOptions = ['Draft', 'Published', 'Archived'];

/// Mockup 06 — IZRAĐENI PLANOVI.
class MentorPublishedPlansScreen extends StatefulWidget {
  const MentorPublishedPlansScreen({super.key});

  @override
  State<MentorPublishedPlansScreen> createState() => _MentorPublishedPlansScreenState();
}

class _MentorPublishedPlansScreenState extends State<MentorPublishedPlansScreen> {
  final _service = MentorService();
  final _searchController = TextEditingController();
  bool _loading = true;
  List<Map<String, dynamic>> _plans = const [];
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
      final plans = await _service.getPlans(search: _searchController.text, status: _statusFilter);
      if (!mounted) return;
      setState(() {
        _plans = plans;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _edit(Map<String, dynamic> plan) async {
    final changed = await showPlanBuilderDialog(context, planId: plan['id'] as int, clientFullName: plan['clientFullName'] as String? ?? '');
    if (changed == true) _load();
  }

  Future<void> _archive(Map<String, dynamic> plan) async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Arhiviraj plan',
      message: 'Da li želite arhivirati plan za ${plan['clientFullName']}? Klijent više neće moći bilježiti nove treninge dok plan ne bude ponovo objavljen.',
      confirmLabel: 'ARHIVIRAJ',
      danger: true,
    );
    if (!confirmed) return;
    try {
      await _service.archivePlan(plan['id'] as int);
      if (!mounted) return;
      showSuccessSnack(context, 'Plan za ${plan['clientFullName']} je arhiviran.');
      _load();
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _republish(Map<String, dynamic> plan) async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Objavi plan ponovo',
      message: 'Klijent (${plan['clientFullName']}) će biti obaviješten da je plan ponovo dostupan. Da li želite nastaviti?',
      confirmLabel: 'OBJAVI PONOVO',
    );
    if (!confirmed) return;
    try {
      await _service.publishPlan(plan['id'] as int);
      if (!mounted) return;
      showSuccessSnack(context, 'Plan za ${plan['clientFullName']} je ponovo objavljen.');
      _load();
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  @override
  Widget build(BuildContext context) {
    return ContentPanel(
      title: 'IZRAĐENI PLANOVI',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(children: [
            Expanded(child: SearchField(controller: _searchController, hintText: 'PRETRAŽI KLIJENTE', onSubmitted: (_) => _load())),
            const SizedBox(width: 12),
            SizedBox(
              width: 200,
              child: DropdownButtonFormField<String?>(
                initialValue: _statusFilter,
                decoration: const InputDecoration(labelText: 'Status'),
                items: [
                  const DropdownMenuItem(value: null, child: Text('Svi statusi')),
                  ..._statusOptions.map((s) => DropdownMenuItem(value: s, child: Text(PlanStatusPresentation.label(s)))),
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
                : _plans.isEmpty
                    ? const EmptyState(message: 'Nema izrađenih planova.')
                    : ListView.separated(
                        itemCount: _plans.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 10),
                        itemBuilder: (context, index) {
                          final plan = _plans[index];
                          final status = plan['status'] as String? ?? '';
                          final canEdit = plan['canEdit'] != false;
                          return Container(
                            padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 14),
                            decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(14)),
                            child: Row(
                              children: [
                                SizedBox(width: 34, child: Text('${index + 1}.', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold))),
                                Expanded(child: Text(plan['clientFullName'] as String? ?? '', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold))),
                                StatusChip(label: PlanStatusPresentation.label(status), color: PlanStatusPresentation.color(status)),
                                const SizedBox(width: 10),
                                Text('${plan['filledDays'] ?? 0}/7 dana', style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
                                const SizedBox(width: 12),
                                PillButton(
                                  label: 'PREGLED..',
                                  onPressed: () => showPlanViewDialog(context, planId: plan['id'] as int, clientFullName: plan['clientFullName'] as String? ?? ''),
                                ),
                                const SizedBox(width: 10),
                                Tooltip(
                                  message: canEdit ? 'Uredi plan' : 'Pretplata više nije aktivna — plan se ne može uređivati.',
                                  child: PillButton(label: 'UREDI PLAN', onPressed: canEdit ? () => _edit(plan) : null),
                                ),
                                if (status == 'Published') ...[
                                  const SizedBox(width: 10),
                                  PillButton(label: 'ARHIVIRAJ', color: AppColors.danger, onPressed: () => _archive(plan)),
                                ],
                                if (status == 'Archived' && canEdit) ...[
                                  const SizedBox(width: 10),
                                  PillButton(label: 'OBJAVI PONOVO', onPressed: () => _republish(plan)),
                                ],
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
