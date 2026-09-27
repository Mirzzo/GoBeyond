import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/network/dio_client.dart';
import '../../../core/theme/app_theme.dart';
import '../../../data/models/progress_model.dart';
import '../../../data/repositories/progress_repository.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/section_header.dart';

class ProgressScreen extends StatefulWidget {
  const ProgressScreen({super.key});

  @override
  State<ProgressScreen> createState() => _ProgressScreenState();
}

class _ProgressScreenState extends State<ProgressScreen> {
  final ProgressRepository _repository = ProgressRepository(DioClient());
  final List<String> _windows = const ['30d', '90d', 'All'];
  ProgressHistoryModel? _history;
  String _selectedWindow = '30d';
  String _searchQuery = '';
  bool _isLoading = true;
  bool _isUploading = false;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _loadProgress();
  }

  Future<void> _loadProgress() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final history =
          await _repository.getProgressHistory(search: _searchQuery);
      if (!mounted) {
        return;
      }

      setState(() {
        _history = history;
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

  List<ActivityEntryModel> get _filteredHistory {
    final entries = _history?.entries ?? const <ActivityEntryModel>[];
    final normalizedQuery = _searchQuery.trim().toLowerCase();

    return entries.where((entry) {
      final days = _selectedWindow == '30d'
          ? 30
          : _selectedWindow == '90d'
              ? 90
              : null;
      if (days != null &&
          DateTime(entry.year, entry.month + 1)
              .isBefore(DateTime.now().subtract(Duration(days: days)))) {
        return false;
      }
      if (normalizedQuery.isEmpty) {
        return true;
      }

      return entry.title.toLowerCase().contains(normalizedQuery) ||
          entry.subtitle.toLowerCase().contains(normalizedQuery) ||
          entry.metric.toLowerCase().contains(normalizedQuery);
    }).toList();
  }

  Future<void> _uploadPhoto() async {
    final photo = await ImagePicker()
        .pickImage(source: ImageSource.gallery, imageQuality: 75);
    if (photo == null || !mounted) return;

    setState(() => _isUploading = true);
    try {
      await _repository.uploadPhotoFile(await photo.readAsBytes(), photo.name);
      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Progress photo saved.')),
      );
      await _loadProgress();
    } catch (error) {
      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Photo upload failed: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _isUploading = false);
      }
    }
  }

  Future<void> _addCheckIn() async {
    final formKey = GlobalKey<FormState>();
    final weight = TextEditingController();
    final measurements = TextEditingController();
    final strength = TextEditingController();
    final conditioning = TextEditingController();
    final saved = await showDialog<bool>(
            context: context,
            builder: (dialogContext) => AlertDialog(
                  title: const Text('Mjesečni napredak'),
                  content: SingleChildScrollView(
                      child: Form(
                          key: formKey,
                          child:
                              Column(mainAxisSize: MainAxisSize.min, children: [
                            TextFormField(
                                controller: weight,
                                keyboardType:
                                    const TextInputType.numberWithOptions(
                                        decimal: true),
                                decoration: const InputDecoration(
                                    labelText: 'Težina (kg)'),
                                validator: (value) {
                                  if ((value ?? '').isEmpty) return null;
                                  final number = double.tryParse(
                                      value!.replaceAll(',', '.'));
                                  return number == null ||
                                          number < 20 ||
                                          number > 500
                                      ? 'Unesite 20–500 kg.'
                                      : null;
                                }),
                            TextFormField(
                                controller: measurements,
                                decoration: const InputDecoration(
                                    labelText: 'Mjere / parametri')),
                            TextFormField(
                                controller: strength,
                                decoration:
                                    const InputDecoration(labelText: 'Snaga')),
                            TextFormField(
                                controller: conditioning,
                                decoration: const InputDecoration(
                                    labelText: 'Kondicija')),
                          ]))),
                  actions: [
                    TextButton(
                        onPressed: () => Navigator.pop(dialogContext, false),
                        child: const Text('Odustani')),
                    ElevatedButton(
                        onPressed: () {
                          if (formKey.currentState!.validate() &&
                              [weight, measurements, strength, conditioning]
                                  .any((controller) =>
                                      controller.text.trim().isNotEmpty))
                            Navigator.pop(dialogContext, true);
                        },
                        child: const Text('Sačuvaj')),
                  ],
                )) ??
        false;
    if (saved && mounted) {
      try {
        await _repository.createProgressEntry({
          if (weight.text.isNotEmpty)
            'weight': double.parse(weight.text.replaceAll(',', '.')),
          'measurements': measurements.text.trim(),
          'strength': strength.text.trim(),
          'conditioning': conditioning.text.trim(),
        });
        await _loadProgress();
        if (mounted)
          ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(content: Text('Mjesečni napredak je sačuvan.')));
      } catch (error) {
        if (mounted)
          ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(content: Text('Napredak nije sačuvan: $error')));
      }
    }
    for (final controller in [weight, measurements, strength, conditioning]) {
      controller.dispose();
    }
  }

  Future<void> _showPlan(ActivityEntryModel entry) async {
    try {
      final response = await _repository.getPlanForEntry(entry.id);
      final plan = response['plan'] as Map<String, dynamic>?;
      if (!mounted) return;
      await showDialog<void>(
          context: context,
          builder: (context) => AlertDialog(
                title: const Text('Plan u vrijeme napretka'),
                content: SingleChildScrollView(
                    child: plan == null
                        ? const Text('Za ovaj mjesec nema objavljenog plana.')
                        : Column(
                            mainAxisSize: MainAxisSize.min,
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                                Text(
                                    plan['focusTitle']?.toString() ??
                                        'Trening plan',
                                    style:
                                        Theme.of(context).textTheme.titleLarge),
                                Text(plan['focusSummary']?.toString() ?? ''),
                                ...((plan['days'] as List<dynamic>? ?? [])
                                        .whereType<Map<String, dynamic>>())
                                    .map((day) => Padding(
                                          padding:
                                              const EdgeInsets.only(top: 12),
                                          child: Text(
                                              '${day['dayOfWeek'] ?? ''}: ${day['trainingDescription'] ?? ''}\nIshrana: ${day['nutritionDescription'] ?? ''}'),
                                        )),
                              ])),
                actions: [
                  TextButton(
                      onPressed: () => Navigator.pop(context),
                      child: const Text('Zatvori'))
                ],
              ));
    } catch (error) {
      if (mounted)
        ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('Historija plana nije dostupna: $error')));
    }
  }

  @override
  Widget build(BuildContext context) {
    final metrics = _history?.metrics ?? const <ProgressMetricModel>[];

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(20, 18, 20, 0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Progress', style: Theme.of(context).textTheme.displaySmall),
            const SizedBox(height: 6),
            Text(
              'Monthly metrics, searchable history and real progress-photo updates.',
              style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                    color: AppTheme.textMutedColor,
                  ),
            ),
            const SizedBox(height: 20),
            SizedBox(
              height: 42,
              child: ListView.separated(
                scrollDirection: Axis.horizontal,
                itemCount: _windows.length,
                separatorBuilder: (_, __) => const SizedBox(width: 8),
                itemBuilder: (context, index) {
                  final window = _windows[index];
                  return ChoiceChip(
                    label: Text(window),
                    selected: _selectedWindow == window,
                    onSelected: (_) {
                      setState(() {
                        _selectedWindow = window;
                      });
                    },
                  );
                },
              ),
            ),
            const SizedBox(height: 16),
            if (_isLoading)
              const Expanded(child: Center(child: CircularProgressIndicator()))
            else if (_errorMessage != null)
              Expanded(
                child: Center(
                  child: Text(
                    _errorMessage!,
                    style: const TextStyle(color: Colors.redAccent),
                  ),
                ),
              )
            else ...[
              SizedBox(
                height: 132,
                child: ListView.separated(
                  scrollDirection: Axis.horizontal,
                  itemCount: metrics.length,
                  separatorBuilder: (_, __) => const SizedBox(width: 12),
                  itemBuilder: (context, index) {
                    final metric = metrics[index];
                    final accentColor = Color(metric.accentColorValue);

                    return SizedBox(
                      width: 172,
                      child: AppPanel(
                        color: AppTheme.surfaceColor,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              metric.label,
                              style: Theme.of(context)
                                  .textTheme
                                  .bodyMedium
                                  ?.copyWith(
                                    color: AppTheme.textMutedColor,
                                  ),
                            ),
                            const Spacer(),
                            Text(
                              metric.value,
                              style: Theme.of(context)
                                  .textTheme
                                  .headlineMedium
                                  ?.copyWith(
                                    color: accentColor,
                                  ),
                            ),
                            const SizedBox(height: 8),
                            Text(metric.trend),
                          ],
                        ),
                      ),
                    );
                  },
                ),
              ),
              const SizedBox(height: 20),
              OutlinedButton.icon(
                  onPressed: _addCheckIn,
                  icon: const Icon(Icons.add_chart),
                  label: const Text('Dodaj mjesečne parametre')),
              const SizedBox(height: 10),
              AppPanel(
                color: AppTheme.surfaceColor,
                child: Row(
                  children: [
                    const Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Photo check-in'),
                          SizedBox(height: 6),
                          Text(
                            'Odaberite fotografiju iz galerije za tekući mjesec.',
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 12),
                    ElevatedButton(
                      onPressed: _isUploading ? null : _uploadPhoto,
                      child: _isUploading
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Text('Upload'),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 18),
              TextField(
                onChanged: (value) {
                  setState(() {
                    _searchQuery = value;
                  });
                },
                onSubmitted: (_) => _loadProgress(),
                decoration: InputDecoration(
                  prefixIcon: const Icon(Icons.search_rounded),
                  hintText: 'Search activity history ($_selectedWindow)',
                  suffixIcon: IconButton(
                    onPressed: _loadProgress,
                    icon: const Icon(Icons.search_rounded),
                  ),
                ),
              ),
              const SizedBox(height: 18),
              SectionHeader(
                title: '${_filteredHistory.length} activity entries',
                subtitle:
                    'History is filterable to match the mobile requirements around activity review.',
              ),
              const SizedBox(height: 12),
              Expanded(
                child: ListView.separated(
                  itemCount: _filteredHistory.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 12),
                  itemBuilder: (context, index) {
                    final entry = _filteredHistory[index];
                    final accentColor = entry.positive
                        ? AppTheme.secondaryColor
                        : const Color(0xFFF06D6D);

                    return AppPanel(
                      color: AppTheme.surfaceColor,
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Container(
                            width: 42,
                            height: 42,
                            decoration: BoxDecoration(
                              color: accentColor.withValues(alpha: 0.14),
                              borderRadius: BorderRadius.circular(14),
                            ),
                            child: Icon(
                              entry.positive
                                  ? Icons.trending_up_rounded
                                  : Icons.update_disabled_rounded,
                              color: accentColor,
                            ),
                          ),
                          const SizedBox(width: 14),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(entry.title,
                                    style: Theme.of(context)
                                        .textTheme
                                        .titleMedium),
                                const SizedBox(height: 6),
                                Text(
                                  entry.subtitle,
                                  style: Theme.of(context)
                                      .textTheme
                                      .bodyMedium
                                      ?.copyWith(
                                        color: AppTheme.textMutedColor,
                                      ),
                                ),
                                const SizedBox(height: 10),
                                Text(
                                  '${entry.whenLabel} | ${entry.metric}',
                                  style: Theme.of(context)
                                      .textTheme
                                      .labelLarge
                                      ?.copyWith(
                                        color: accentColor,
                                      ),
                                ),
                                if (entry.photoUrl != null &&
                                    entry.photoUrl!.isNotEmpty)
                                  Padding(
                                      padding: const EdgeInsets.only(top: 10),
                                      child: Image.network(entry.photoUrl!,
                                          height: 90,
                                          fit: BoxFit.cover,
                                          errorBuilder: (_, __, ___) => const Text(
                                              'Fotografija trenutno nije dostupna.'))),
                                TextButton(
                                    onPressed: () => _showPlan(entry),
                                    child: const Text('Historija plana')),
                              ],
                            ),
                          ),
                        ],
                      ),
                    );
                  },
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
