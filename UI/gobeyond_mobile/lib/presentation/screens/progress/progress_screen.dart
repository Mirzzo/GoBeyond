import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../demo/mobile_demo_data.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/section_header.dart';

class ProgressScreen extends StatefulWidget {
  const ProgressScreen({super.key});

  @override
  State<ProgressScreen> createState() => _ProgressScreenState();
}

class _ProgressScreenState extends State<ProgressScreen> {
  final List<String> _windows = const ['30d', '90d', 'All'];
  String _selectedWindow = '30d';
  String _searchQuery = '';

  List<ActivityEntry> get _filteredHistory {
    final normalizedQuery = _searchQuery.trim().toLowerCase();

    return MobileDemoData.activityHistory.where((entry) {
      if (normalizedQuery.isEmpty) {
        return true;
      }

      return entry.title.toLowerCase().contains(normalizedQuery) ||
          entry.subtitle.toLowerCase().contains(normalizedQuery) ||
          entry.metric.toLowerCase().contains(normalizedQuery);
    }).toList();
  }

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(20, 18, 20, 0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Progress', style: Theme.of(context).textTheme.displaySmall),
            const SizedBox(height: 6),
            Text(
              'Monthly metrics, searchable history and photo update placeholder.',
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
            SizedBox(
              height: 132,
              child: ListView.separated(
                scrollDirection: Axis.horizontal,
                itemCount: MobileDemoData.progressMetrics.length,
                separatorBuilder: (_, __) => const SizedBox(width: 12),
                itemBuilder: (context, index) {
                  final metric = MobileDemoData.progressMetrics[index];
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
                          'Progress photos are still placeholder-only, but the upload action exists in UI.',
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 12),
                  ElevatedButton(
                    onPressed: () {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(content: Text('Photo upload placeholder opened.')),
                      );
                    },
                    child: const Text('Upload'),
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
              decoration: InputDecoration(
                prefixIcon: const Icon(Icons.search_rounded),
                hintText: 'Search activity history ($_selectedWindow)',
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
        ),
      ),
    );
  }
}
