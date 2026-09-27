import 'package:flutter/material.dart';

import '../../../../core/services/mentor_service.dart';
import '../../../../core/theme/app_theme.dart';
import '../../../../core/utils/api_error.dart';
import '../../../widgets/dialogs.dart';

/// Mockup 05 — IZRADI dialog for a single day: TRENING / ISHRANA tabs,
/// duration pickers (hours+minutes, no free-text) and a large OPIS field.
/// Returns the updated `PlanDetail` map on success.
Future<Map<String, dynamic>?> showPlanDayDialog(
  BuildContext context, {
  required int planId,
  required int dayOfWeek,
  required String dayName,
  Map<String, dynamic>? existingDay,
  bool allowDelete = false,
}) {
  return showDialog<Map<String, dynamic>>(
    context: context,
    builder: (_) => _PlanDayDialog(
      planId: planId,
      dayOfWeek: dayOfWeek,
      dayName: dayName,
      existingDay: existingDay,
      allowDelete: allowDelete,
    ),
  );
}

class _PlanDayDialog extends StatefulWidget {
  const _PlanDayDialog({
    required this.planId,
    required this.dayOfWeek,
    required this.dayName,
    this.existingDay,
    this.allowDelete = false,
  });

  final int planId;
  final int dayOfWeek;
  final String dayName;
  final Map<String, dynamic>? existingDay;
  final bool allowDelete;

  @override
  State<_PlanDayDialog> createState() => _PlanDayDialogState();
}

class _PlanDayDialogState extends State<_PlanDayDialog> with SingleTickerProviderStateMixin {
  final _service = MentorService();
  late final TabController _tabController;
  final _trainingDescriptionController = TextEditingController();
  final _nutritionDescriptionController = TextEditingController();

  int _trainingHours = 1;
  int _trainingMinutes = 0;
  int _nutritionHours = 0;
  int _nutritionMinutes = 0;
  bool _nutritionHasDuration = false;
  bool _saving = false;
  String? _trainingError;
  String? _nutritionError;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    final existing = widget.existingDay;
    if (existing != null) {
      final trainingMinutes = existing['trainingDurationMinutes'] as int? ?? 60;
      _trainingHours = trainingMinutes ~/ 60;
      _trainingMinutes = trainingMinutes % 60;
      _trainingDescriptionController.text = existing['trainingDescription'] as String? ?? '';

      final nutritionMinutes = existing['nutritionDurationMinutes'] as int?;
      if (nutritionMinutes != null) {
        _nutritionHasDuration = true;
        _nutritionHours = nutritionMinutes ~/ 60;
        _nutritionMinutes = nutritionMinutes % 60;
      }
      _nutritionDescriptionController.text = existing['nutritionDescription'] as String? ?? '';
    } else {
      _trainingDescriptionController.text = '';
      _nutritionDescriptionController.text = '';
    }
  }

  @override
  void dispose() {
    _tabController.dispose();
    _trainingDescriptionController.dispose();
    _nutritionDescriptionController.dispose();
    super.dispose();
  }

  int get _trainingTotalMinutes => _trainingHours * 60 + _trainingMinutes;
  int get _nutritionTotalMinutes => _nutritionHours * 60 + _nutritionMinutes;

  String _hms(int totalMinutes) {
    final h = totalMinutes ~/ 60;
    final m = totalMinutes % 60;
    return '${h.toString().padLeft(2, '0')}:${m.toString().padLeft(2, '0')}:00';
  }

  Future<void> _save() async {
    setState(() {
      _trainingError = null;
      _nutritionError = null;
    });

    final trainingMinutes = _trainingTotalMinutes;
    final trainingDescription = _trainingDescriptionController.text.trim();
    final nutritionDescription = _nutritionDescriptionController.text.trim();

    bool hasError = false;
    if (trainingMinutes < 1 || trainingMinutes > 600) {
      setState(() => _trainingError = 'Trajanje treninga mora biti između 1 minute i 10 sati.');
      hasError = true;
    }
    if (trainingDescription.length < 10 || trainingDescription.length > 8000) {
      setState(() => _trainingError = 'Opis treninga mora imati između 10 i 8000 znakova.');
      hasError = true;
    }
    if (nutritionDescription.length < 10 || nutritionDescription.length > 8000) {
      setState(() => _nutritionError = 'Opis ishrane mora imati između 10 i 8000 znakova.');
      hasError = true;
    }
    final nutritionMinutes = _nutritionHasDuration ? _nutritionTotalMinutes : null;
    if (nutritionMinutes != null && (nutritionMinutes < 1 || nutritionMinutes > 1440)) {
      setState(() => _nutritionError = 'Trajanje ishrane mora biti između 1 minute i 24 sata.');
      hasError = true;
    }

    if (hasError) {
      if (_trainingError != null) _tabController.index = 0;
      if (_nutritionError != null && _trainingError == null) _tabController.index = 1;
      return;
    }

    setState(() => _saving = true);
    try {
      final updated = await _service.saveDay(
        widget.planId,
        widget.dayOfWeek,
        trainingDurationMinutes: trainingMinutes,
        trainingDescription: trainingDescription,
        nutritionDurationMinutes: nutritionMinutes,
        nutritionDescription: nutritionDescription,
      );
      if (!mounted) return;
      showSuccessSnack(context, 'Plan za ${widget.dayName.toLowerCase()} je sačuvan.');
      Navigator.of(context).pop(updated);
    } catch (error) {
      final apiError = ApiError.from(error);
      if (!mounted) return;
      setState(() {
        _trainingError = apiError.fieldErrors['trainingDescription'] ?? apiError.fieldErrors['trainingDurationMinutes'];
        _nutritionError = apiError.fieldErrors['nutritionDescription'] ?? apiError.fieldErrors['nutritionDurationMinutes'];
      });
      if (_trainingError != null) _tabController.index = 0;
      if (_nutritionError != null && _trainingError == null) _tabController.index = 1;
      showErrorSnack(context, apiError.message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _delete() async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Ukloni dan',
      message: 'Da li želite ukloniti unesen plan za ${widget.dayName.toLowerCase()}?',
      confirmLabel: 'UKLONI',
      danger: true,
    );
    if (!confirmed) return;
    setState(() => _saving = true);
    try {
      final updated = await _service.deleteDay(widget.planId, widget.dayOfWeek);
      if (!mounted) return;
      showSuccessSnack(context, 'Plan za ${widget.dayName.toLowerCase()} je uklonjen.');
      Navigator.of(context).pop(updated);
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return GbDialog(
      title: widget.dayName.toUpperCase(),
      width: 640,
      actions: [
        if (widget.allowDelete && widget.existingDay != null) ...[
          OutlinedButton(
            onPressed: _saving ? null : _delete,
            style: OutlinedButton.styleFrom(foregroundColor: AppColors.danger, side: const BorderSide(color: AppColors.danger)),
            child: const Text('UKLONI DAN'),
          ),
          const Spacer(),
        ],
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Odustani')),
        const SizedBox(width: 8),
        ElevatedButton(
          onPressed: _saving ? null : _save,
          child: _saving
              ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.black))
              : const Text('SPREMI'),
        ),
      ],
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TabBar(
            controller: _tabController,
            labelColor: Colors.black,
            unselectedLabelColor: Colors.white,
            indicator: BoxDecoration(color: AppColors.accent, borderRadius: BorderRadius.circular(8)),
            tabs: const [Tab(text: 'TRENING'), Tab(text: 'ISHRANA')],
          ),
          const SizedBox(height: 20),
          SizedBox(
            height: 420,
            child: TabBarView(
              controller: _tabController,
              children: [
                _durationAndDescriptionForm(
                  hours: _trainingHours,
                  minutes: _trainingMinutes,
                  maxHours: 10,
                  onHoursChanged: (v) => setState(() => _trainingHours = v),
                  onMinutesChanged: (v) => setState(() => _trainingMinutes = v),
                  descriptionController: _trainingDescriptionController,
                  error: _trainingError,
                  optionalToggle: false,
                  toggleValue: true,
                  onToggleChanged: null,
                ),
                _durationAndDescriptionForm(
                  hours: _nutritionHours,
                  minutes: _nutritionMinutes,
                  maxHours: 24,
                  onHoursChanged: (v) => setState(() => _nutritionHours = v),
                  onMinutesChanged: (v) => setState(() => _nutritionMinutes = v),
                  descriptionController: _nutritionDescriptionController,
                  error: _nutritionError,
                  optionalToggle: true,
                  toggleValue: _nutritionHasDuration,
                  onToggleChanged: (v) => setState(() => _nutritionHasDuration = v),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _durationAndDescriptionForm({
    required int hours,
    required int minutes,
    required int maxHours,
    required ValueChanged<int> onHoursChanged,
    required ValueChanged<int> onMinutesChanged,
    required TextEditingController descriptionController,
    required String? error,
    required bool optionalToggle,
    required bool toggleValue,
    required ValueChanged<bool>? onToggleChanged,
  }) {
    final totalMinutes = hours * 60 + minutes;
    return SingleChildScrollView(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (optionalToggle)
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Odredi trajanje ishrane (opciono)', style: TextStyle(color: Colors.white, fontSize: 13)),
              value: toggleValue,
              onChanged: onToggleChanged,
            ),
          if (!optionalToggle || toggleValue) ...[
            const Text('TRAJANJE', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold)),
            const SizedBox(height: 8),
            Row(children: [
              Expanded(
                child: DropdownButtonFormField<int>(
                  initialValue: hours,
                  decoration: const InputDecoration(labelText: 'Sati'),
                  items: List.generate(maxHours + 1, (i) => i).map((h) => DropdownMenuItem(value: h, child: Text('$h h'))).toList(),
                  onChanged: (v) => onHoursChanged(v ?? 0),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: DropdownButtonFormField<int>(
                  initialValue: minutes,
                  decoration: const InputDecoration(labelText: 'Minute'),
                  items: [0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55]
                      .map((m) => DropdownMenuItem(value: m, child: Text('$m min')))
                      .toList(),
                  onChanged: (v) => onMinutesChanged(v ?? 0),
                ),
              ),
            ]),
            const SizedBox(height: 6),
            Text('Ukupno trajanje: ${_hms(totalMinutes)}', style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
            const SizedBox(height: 16),
          ],
          const Text('OPIS', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold)),
          const SizedBox(height: 8),
          TextField(
            controller: descriptionController,
            maxLines: 8,
            minLines: 6,
            style: const TextStyle(color: Colors.white),
            decoration: const InputDecoration(hintText: 'Unesite detaljan opis (10-8000 znakova)...'),
          ),
          if (error != null) ...[
            const SizedBox(height: 6),
            Text(error, style: const TextStyle(color: AppColors.danger, fontSize: 12, fontWeight: FontWeight.w600)),
          ],
        ],
      ),
    );
  }
}
