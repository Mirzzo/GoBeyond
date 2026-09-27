import 'package:flutter/material.dart';

import '../../../../core/services/mentor_service.dart';
import '../../../../core/theme/app_theme.dart';
import '../../../../core/utils/api_error.dart';
import '../../../../core/utils/formatters.dart';
import '../../../widgets/dialogs.dart';
import '../../../widgets/panel.dart';

/// Read-only PREGLED for an existing plan (mockup 06). Shows the
/// motivational quote and each filled day's training/nutrition content.
Future<void> showPlanViewDialog(BuildContext context, {required int planId, required String clientFullName}) {
  return showDialog<void>(
    context: context,
    builder: (_) => _PlanViewDialog(planId: planId, clientFullName: clientFullName),
  );
}

class _PlanViewDialog extends StatefulWidget {
  const _PlanViewDialog({required this.planId, required this.clientFullName});
  final int planId;
  final String clientFullName;

  @override
  State<_PlanViewDialog> createState() => _PlanViewDialogState();
}

class _PlanViewDialogState extends State<_PlanViewDialog> {
  final _service = MentorService();
  bool _loading = true;
  String? _error;
  Map<String, dynamic>? _plan;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final plan = await _service.getPlan(widget.planId);
      if (!mounted) return;
      setState(() {
        _plan = plan;
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

  @override
  Widget build(BuildContext context) {
    return GbDialog(
      title: 'Plan — ${widget.clientFullName}',
      width: 640,
      actions: [ElevatedButton(onPressed: () => Navigator.of(context).pop(), child: const Text('ZATVORI'))],
      child: _loading
          ? const SizedBox(height: 200, child: Center(child: CircularProgressIndicator()))
          : _error != null
              ? SizedBox(height: 100, child: Center(child: Text(_error!, style: const TextStyle(color: AppColors.danger))))
              : _buildContent(_plan!),
    );
  }

  Widget _buildContent(Map<String, dynamic> plan) {
    final status = plan['status'] as String? ?? '';
    final days = (plan['days'] as List<dynamic>? ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList()
      ..sort((a, b) => (a['dayOfWeek'] as int).compareTo(b['dayOfWeek'] as int));

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(children: [
          StatusChip(label: PlanStatusPresentation.label(status), color: PlanStatusPresentation.color(status)),
          const SizedBox(width: 12),
          Text('Verzija ${plan['version'] ?? 1}', style: const TextStyle(color: AppColors.textMuted)),
          const Spacer(),
          if (plan['publishedAt'] != null) Text('Objavljen: ${Formatters.date(plan['publishedAt'] as String?)}', style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
        ]),
        if ((plan['motivationalQuote'] as String?)?.isNotEmpty == true) ...[
          const SizedBox(height: 14),
          Container(
            padding: const EdgeInsets.all(12),
            width: double.infinity,
            decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(10)),
            child: Text('"${plan['motivationalQuote']}"', style: const TextStyle(color: AppColors.accent, fontStyle: FontStyle.italic)),
          ),
        ],
        const SizedBox(height: 16),
        if (days.isEmpty)
          const Text('Plan još nema popunjenih dana.', style: TextStyle(color: AppColors.textMuted))
        else
          ...days.map((day) => Container(
                margin: const EdgeInsets.only(bottom: 10),
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(12)),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(day['dayName'] as String? ?? '', style: const TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold)),
                    const SizedBox(height: 8),
                    Text('TRENING (${Formatters.minutesToHms(day['trainingDurationMinutes'] as int? ?? 0)})',
                        style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600, fontSize: 12)),
                    Text(day['trainingDescription'] as String? ?? '', style: const TextStyle(color: Colors.white)),
                    const SizedBox(height: 8),
                    Text(
                      day['nutritionDurationMinutes'] != null
                          ? 'ISHRANA (${Formatters.minutesToHms(day['nutritionDurationMinutes'] as int)})'
                          : 'ISHRANA',
                      style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600, fontSize: 12),
                    ),
                    Text(day['nutritionDescription'] as String? ?? '', style: const TextStyle(color: Colors.white)),
                  ],
                ),
              )),
      ],
    );
  }
}
