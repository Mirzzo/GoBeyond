import 'dart:async';

import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/utils/validators.dart';
import '../../../data/models/progress_entry.dart';
import '../../../data/models/training_plan.dart';
import '../../../data/repositories/progress_repository.dart';
import '../../widgets/app_dialogs.dart';
import '../../widgets/app_modal_page.dart';
import '../../widgets/app_network_image.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/gb_scaffold.dart';
import '../../widgets/primary_button.dart';
import '../../widgets/state_views.dart';
import '../../widgets/truncated_description.dart';
import '../plan/plan_full_text_screen.dart';

/// Mockup 14: GODINA/MJESEC pickers, the month's photo + Težina/Obimi/Snaga/
/// Kondicija, HISTORIJA PLANA popup, and a weight-over-time chart.
class TrainingHistoryScreen extends StatefulWidget {
  const TrainingHistoryScreen({super.key, this.progressRepository});

  final ProgressRepository? progressRepository;

  @override
  State<TrainingHistoryScreen> createState() => _TrainingHistoryScreenState();
}

class _TrainingHistoryScreenState extends State<TrainingHistoryScreen> {
  late final ProgressRepository _repository =
      widget.progressRepository ?? ApiProgressRepository();
  final _picker = ImagePicker();

  late int _selectedYear = DateTime.now().year;
  late int _selectedMonth = DateTime.now().month;

  late Future<List<int>> _yearsFuture;
  Future<ProgressEntryItem?>? _entryFuture;
  Future<List<WeightPoint>>? _chartFuture;
  bool _uploadingPhoto = false;

  @override
  void initState() {
    super.initState();
    // Set the initial futures directly (not via _reloadEntry/setState — the
    // first build hasn't happened yet, so there is nothing to re-render).
    _entryFuture = _repository.getEntry(_selectedYear, _selectedMonth);
    _chartFuture = _repository.getChart();
    _yearsFuture = _repository.getYears().then((years) {
      if (years.isNotEmpty && !years.contains(_selectedYear)) {
        _selectedYear = years.first;
        // This runs after the first build completed, so re-fetching the
        // entry for the corrected year needs an explicit setState.
        if (mounted) {
          setState(() {
            _entryFuture = _repository.getEntry(_selectedYear, _selectedMonth);
          });
        }
      }
      return years;
    });
  }

  void _reloadEntry() {
    setState(() {
      _entryFuture = _repository.getEntry(_selectedYear, _selectedMonth);
    });
  }

  bool get _isFutureMonth {
    final now = DateTime.now();
    return _selectedYear > now.year ||
        (_selectedYear == now.year && _selectedMonth > now.month);
  }

  Future<void> _uploadPhoto() async {
    final file =
        await _picker.pickImage(source: ImageSource.gallery, imageQuality: 85);
    if (file == null) return;

    setState(() => _uploadingPhoto = true);
    try {
      final bytes = await file.readAsBytes();
      await _repository.uploadPhoto(
          _selectedYear, _selectedMonth, bytes, file.name);
      if (!mounted) return;
      showSuccessSnackBar(context, 'Slika za odabrani mjesec je sačuvana.');
      _reloadEntry();
    } catch (error) {
      if (!mounted) return;
      showErrorSnackBar(context, ApiException.from(error).message);
    } finally {
      if (mounted) setState(() => _uploadingPhoto = false);
    }
  }

  Future<void> _openPlanHistory() async {
    try {
      final plan =
          await _repository.getPlanSnapshot(_selectedYear, _selectedMonth);
      if (!mounted) return;
      if (plan == null) {
        showErrorSnackBar(context, 'Nema sačuvanog plana za odabrani mjesec.');
        return;
      }
      Navigator.of(context).push(
        MaterialPageRoute(builder: (_) => _PlanSnapshotScreen(plan: plan)),
      );
    } catch (error) {
      if (!mounted) return;
      showErrorSnackBar(context, ApiException.from(error).message);
    }
  }

  @override
  Widget build(BuildContext context) {
    return GbScaffold(
      title: 'Historija treninga',
      body: FutureBuilder<List<int>>(
        future: _yearsFuture,
        builder: (context, yearsSnapshot) {
          if (yearsSnapshot.connectionState != ConnectionState.done) {
            return const LoadingView();
          }
          if (yearsSnapshot.hasError) {
            return ErrorView(
                message: ApiException.from(yearsSnapshot.error!).message);
          }
          final years = yearsSnapshot.data!.isEmpty
              ? [DateTime.now().year]
              : yearsSnapshot.data!;

          return ListView(
            padding: const EdgeInsets.all(20),
            children: [
              const Text('HISTORIJA TRENINGA',
                  style: TextStyle(fontWeight: FontWeight.w800, fontSize: 18)),
              const SizedBox(height: 16),
              Row(
                children: [
                  Expanded(
                    child: _PickerField(
                      label: 'GODINA',
                      value: '$_selectedYear',
                      onTap: () async {
                        final picked = await _pickFromList<int>(
                          context,
                          title: 'Odaberite godinu',
                          options: years,
                          labelBuilder: (y) => '$y',
                        );
                        if (picked != null && mounted) {
                          setState(() => _selectedYear = picked);
                          _reloadEntry();
                        }
                      },
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: _PickerField(
                      label: 'MJESEC',
                      value: Formatters.monthName(_selectedMonth),
                      onTap: () async {
                        final picked = await _pickFromList<int>(
                          context,
                          title: 'Odaberite mjesec',
                          options: List.generate(12, (i) => i + 1),
                          labelBuilder: Formatters.monthName,
                        );
                        if (picked != null && mounted) {
                          setState(() => _selectedMonth = picked);
                          _reloadEntry();
                        }
                      },
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 18),
              FutureBuilder<ProgressEntryItem?>(
                future: _entryFuture,
                builder: (context, snapshot) {
                  if (snapshot.connectionState != ConnectionState.done) {
                    return const Padding(
                      padding: EdgeInsets.symmetric(vertical: 40),
                      child: LoadingView(),
                    );
                  }
                  if (snapshot.hasError) {
                    return ErrorView(
                      message: ApiException.from(snapshot.error!).message,
                      onRetry: _reloadEntry,
                    );
                  }
                  return _MonthCard(
                    key: ValueKey('$_selectedYear-$_selectedMonth'),
                    year: _selectedYear,
                    month: _selectedMonth,
                    entry: snapshot.data,
                    isFutureMonth: _isFutureMonth,
                    uploadingPhoto: _uploadingPhoto,
                    onUploadPhoto: _uploadPhoto,
                    onOpenPlanHistory: _openPlanHistory,
                    repository: _repository,
                    onSaved: _reloadEntry,
                  );
                },
              ),
              const SizedBox(height: 26),
              const Text('TEŽINA KROZ VRIJEME',
                  style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
              const SizedBox(height: 14),
              FutureBuilder<List<WeightPoint>>(
                future: _chartFuture,
                builder: (context, snapshot) {
                  if (snapshot.connectionState != ConnectionState.done) {
                    return const SizedBox(height: 180, child: LoadingView());
                  }
                  if (snapshot.hasError) {
                    return ErrorView(
                        message: ApiException.from(snapshot.error!).message);
                  }
                  final points = snapshot.data!;
                  if (points.length < 2) {
                    return const AppPanel(
                      child: Text(
                        'Potrebno je barem dva mjesečna unosa da se prikaže grafikon.',
                        style: TextStyle(color: AppTheme.textMuted),
                        textAlign: TextAlign.center,
                      ),
                    );
                  }
                  return _WeightChart(points: points);
                },
              ),
            ],
          );
        },
      ),
    );
  }

  Future<T?> _pickFromList<T>(
    BuildContext context, {
    required String title,
    required List<T> options,
    required String Function(T) labelBuilder,
  }) {
    return showModalBottomSheet<T>(
      context: context,
      backgroundColor: AppTheme.panel,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (context) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Padding(
              padding: const EdgeInsets.all(16),
              child: Text(title,
                  style: const TextStyle(fontWeight: FontWeight.w800)),
            ),
            Flexible(
              child: ListView(
                shrinkWrap: true,
                children: options
                    .map((option) => ListTile(
                          title: Text(labelBuilder(option)),
                          onTap: () => Navigator.of(context).pop(option),
                        ))
                    .toList(),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _PickerField extends StatelessWidget {
  const _PickerField(
      {required this.label, required this.value, required this.onTap});

  final String label;
  final String value;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(18),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        decoration: BoxDecoration(
          color: AppTheme.panelLight,
          borderRadius: BorderRadius.circular(18),
        ),
        child: Row(
          children: [
            Expanded(
              child: Text('$label: $value',
                  style: const TextStyle(
                      fontWeight: FontWeight.w800, fontSize: 13)),
            ),
            const Icon(Icons.expand_more_rounded, size: 20),
          ],
        ),
      ),
    );
  }
}

class _MonthCard extends StatefulWidget {
  const _MonthCard({
    super.key,
    required this.year,
    required this.month,
    required this.entry,
    required this.isFutureMonth,
    required this.uploadingPhoto,
    required this.onUploadPhoto,
    required this.onOpenPlanHistory,
    required this.repository,
    required this.onSaved,
  });

  final int year;
  final int month;
  final ProgressEntryItem? entry;
  final bool isFutureMonth;
  final bool uploadingPhoto;
  final VoidCallback onUploadPhoto;
  final VoidCallback onOpenPlanHistory;
  final ProgressRepository repository;
  final VoidCallback onSaved;

  @override
  State<_MonthCard> createState() => _MonthCardState();
}

class _MonthCardState extends State<_MonthCard> {
  final _formKey = GlobalKey<FormState>();
  late final _weightController =
      TextEditingController(text: widget.entry?.weightKg.toString() ?? '');
  late final _measurementsController =
      TextEditingController(text: widget.entry?.measurements ?? '');
  late final _strengthController =
      TextEditingController(text: widget.entry?.strength ?? '');
  late final _conditioningController =
      TextEditingController(text: widget.entry?.conditioning ?? '');
  bool _saving = false;
  String? _error;

  @override
  void dispose() {
    _weightController.dispose();
    _measurementsController.dispose();
    _strengthController.dispose();
    _conditioningController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await widget.repository.upsertEntry(
        year: widget.year,
        month: widget.month,
        weightKg: num.parse(_weightController.text.trim()),
        measurements: _measurementsController.text.trim(),
        strength: _strengthController.text.trim(),
        conditioning: _conditioningController.text.trim(),
      );
      if (!mounted) return;
      showSuccessSnackBar(context, 'Napredak za odabrani mjesec je sačuvan.');
      widget.onSaved();
    } catch (error) {
      if (!mounted) return;
      setState(() => _error = ApiException.from(error).message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (widget.isFutureMonth) {
      return const AppPanel(
        child: Text(
          'Ne možete unositi podatke za budući mjesec.',
          textAlign: TextAlign.center,
          style: TextStyle(color: AppTheme.textMuted),
        ),
      );
    }

    return AppPanel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Stack(
            alignment: Alignment.center,
            children: [
              ClipRRect(
                borderRadius: BorderRadius.circular(18),
                child: AppNetworkImage(
                  url: widget.entry?.photoUrl,
                  height: 180,
                  borderRadius: 18,
                  placeholderIcon: Icons.image_rounded,
                ),
              ),
              if (widget.uploadingPhoto) const CircularProgressIndicator(),
            ],
          ),
          const SizedBox(height: 10),
          TextButton.icon(
            onPressed: widget.uploadingPhoto ? null : widget.onUploadPhoto,
            icon: const Icon(Icons.add_a_photo_rounded),
            label: const Text('Dodaj/promijeni sliku'),
          ),
          const SizedBox(height: 8),
          Form(
            key: _formKey,
            child: Column(
              children: [
                _MetricField(
                  label: 'Težina (kg)',
                  controller: _weightController,
                  keyboardType:
                      const TextInputType.numberWithOptions(decimal: true),
                  validator: (v) => Validators.numberRange(v,
                      min: 30, max: 300, label: 'Težina'),
                ),
                _MetricField(
                  label: 'Obimi',
                  controller: _measurementsController,
                  validator: (v) => Validators.textLength(v,
                      min: 2, max: 300, label: 'Obimi'),
                ),
                _MetricField(
                  label: 'Snaga',
                  controller: _strengthController,
                  validator: (v) => Validators.textLength(v,
                      min: 2, max: 300, label: 'Snaga'),
                ),
                _MetricField(
                  label: 'Kondicija',
                  controller: _conditioningController,
                  validator: (v) => Validators.textLength(v,
                      min: 2, max: 300, label: 'Kondicija'),
                ),
              ],
            ),
          ),
          if (_error != null) ...[
            const SizedBox(height: 8),
            Text(_error!,
                style: const TextStyle(color: AppTheme.danger, fontSize: 13)),
          ],
          const SizedBox(height: 14),
          PrimaryButton(
            label: widget.entry == null ? 'SAČUVAJ UNOS' : 'SPREMI IZMJENE',
            isLoading: _saving,
            onPressed: _save,
          ),
          const SizedBox(height: 12),
          PrimaryButton(
            label: 'HISTORIJA PLANA',
            onPressed: (widget.entry?.hasPlanSnapshot ?? false)
                ? widget.onOpenPlanHistory
                : null,
          ),
        ],
      ),
    );
  }
}

class _MetricField extends StatelessWidget {
  const _MetricField({
    required this.label,
    required this.controller,
    required this.validator,
    this.keyboardType,
  });

  final String label;
  final TextEditingController controller;
  final String? Function(String?) validator;
  final TextInputType? keyboardType;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 10),
      child: TextFormField(
        controller: controller,
        keyboardType: keyboardType,
        decoration: InputDecoration(labelText: label),
        validator: validator,
      ),
    );
  }
}

class _WeightChart extends StatelessWidget {
  const _WeightChart({required this.points});

  final List<WeightPoint> points;

  @override
  Widget build(BuildContext context) {
    final spots = <FlSpot>[
      for (var i = 0; i < points.length; i++)
        FlSpot(i.toDouble(), points[i].weightKg.toDouble()),
    ];

    return AppPanel(
      child: SizedBox(
        height: 200,
        child: LineChart(
          LineChartData(
            gridData: const FlGridData(show: true, drawVerticalLine: false),
            borderData: FlBorderData(show: false),
            titlesData: FlTitlesData(
              topTitles:
                  const AxisTitles(sideTitles: SideTitles(showTitles: false)),
              rightTitles:
                  const AxisTitles(sideTitles: SideTitles(showTitles: false)),
              leftTitles: AxisTitles(
                sideTitles: SideTitles(
                    showTitles: true,
                    reservedSize: 36,
                    getTitlesWidget: (v, meta) {
                      return Text(v.toStringAsFixed(0),
                          style: const TextStyle(
                              color: AppTheme.textMuted, fontSize: 10));
                    }),
              ),
              bottomTitles: AxisTitles(
                sideTitles: SideTitles(
                  showTitles: true,
                  reservedSize: 28,
                  getTitlesWidget: (v, meta) {
                    final index = v.toInt();
                    if (index < 0 || index >= points.length)
                      return const SizedBox.shrink();
                    final point = points[index];
                    return Padding(
                      padding: const EdgeInsets.only(top: 6),
                      child: Text(
                        '${point.month}/${point.year % 100}',
                        style: const TextStyle(
                            color: AppTheme.textMuted, fontSize: 10),
                      ),
                    );
                  },
                ),
              ),
            ),
            lineBarsData: [
              LineChartBarData(
                spots: spots,
                isCurved: true,
                color: AppTheme.accent,
                barWidth: 3,
                dotData: const FlDotData(show: true),
                belowBarData: BarAreaData(
                  show: true,
                  color: AppTheme.accent.withValues(alpha: 0.15),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Read-only popup for HISTORIJA PLANA: the plan snapshot saved when this
/// month's progress entry was created.
class _PlanSnapshotScreen extends StatelessWidget {
  const _PlanSnapshotScreen({required this.plan});

  final TrainingPlan plan;

  @override
  Widget build(BuildContext context) {
    return AppModalPage(
      title: 'Historija plana',
      body: ListView(
        children: [
          if (plan.motivationalQuote != null &&
              plan.motivationalQuote!.isNotEmpty) ...[
            Text('"${plan.motivationalQuote}"',
                textAlign: TextAlign.center,
                style: const TextStyle(
                    color: AppTheme.accent,
                    fontStyle: FontStyle.italic,
                    fontWeight: FontWeight.w700)),
            const SizedBox(height: 16),
          ],
          for (final day in plan.days)
            Padding(
              padding: const EdgeInsets.only(bottom: 14),
              child: AppPanel(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(day.dayName.toUpperCase(),
                        style: const TextStyle(
                            color: AppTheme.accent,
                            fontWeight: FontWeight.w800)),
                    const SizedBox(height: 4),
                    Text(
                      'TRAJANJE: ${Formatters.durationFromMinutes(day.trainingDurationMinutes)}',
                      style: const TextStyle(
                          fontWeight: FontWeight.w700, fontSize: 12.5),
                    ),
                    const SizedBox(height: 8),
                    TruncatedDescription(
                      text: day.trainingDescription,
                      maxLines: 5,
                      onReadMore: () => Navigator.of(context).push(
                        MaterialPageRoute(
                          builder: (_) => PlanFullTextScreen(
                            title: day.dayName.toUpperCase(),
                            text: day.trainingDescription,
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }
}
