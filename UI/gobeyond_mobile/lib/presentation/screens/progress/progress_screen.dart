import 'package:flutter/material.dart';

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
      final history = await _repository.getProgressHistory(search: _searchQuery);
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
      if (normalizedQuery.isEmpty) {
        return true;
      }

      return entry.title.toLowerCase().contains(normalizedQuery) ||
          entry.subtitle.toLowerCase().contains(normalizedQuery) ||
          entry.metric.toLowerCase().contains(normalizedQuery);
    }).toList();
  }

  Future<void> _uploadPhoto() async {
    final controller = TextEditingController();
    final photoUrl = await showDialog<String>(
      context: context,
      builder: (context) {
        return AlertDialog(
          title: const Text('Upload progress photo'),
          content: TextField(
            controller: controller,
            decoration: const InputDecoration(
              labelText: 'Photo URL',
              hintText: 'https://...',
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(context).pop(),
              child: const Text('Cancel'),
            ),
            ElevatedButton(
              onPressed: () => Navigator.of(context).pop(controller.text.trim()),
              child: const Text('Save'),
            ),
          ],
        );
      },
    );

    controller.dispose();

    if (photoUrl == null || photoUrl.isEmpty || !mounted) {
      return;
    }

    setState(() => _isUploading = true);
    try {
      await _repository.uploadPhoto(photoUrl);
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
                              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                                    color: AppTheme.textMutedColor,
                                  ),
                            ),
                            const Spacer(),
                            Text(
                              metric.value,
                              style: Theme.of(context).textTheme.headlineMedium?.copyWith(
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
                            'Save a real photo URL to the current month progress entry.',
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
                subtitle: 'History is filterable to match the mobile requirements around activity review.',
              ),
              const SizedBox(height: 12),
              Expanded(
                child: ListView.separated(
                  itemCount: _filteredHistory.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 12),
                  itemBuilder: (context, index) {
                    final entry = _filteredHistory[index];
                    final accentColor = entry.positive ? AppTheme.secondaryColor : const Color(0xFFF06D6D);

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
                              entry.positive ? Icons.trending_up_rounded : Icons.update_disabled_rounded,
                              color: accentColor,
                            ),
                          ),
                          const SizedBox(width: 14),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(entry.title, style: Theme.of(context).textTheme.titleMedium),
                                const SizedBox(height: 6),
                                Text(
                                  entry.subtitle,
                                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                                        color: AppTheme.textMutedColor,
                                      ),
                                ),
                                const SizedBox(height: 10),
                                Text(
                                  '${entry.whenLabel} | ${entry.metric}',
                                  style: Theme.of(context).textTheme.labelLarge?.copyWith(
                                        color: accentColor,
                                      ),
                                ),
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
