class ProgressHistoryModel {
  ProgressHistoryModel({
    required this.metrics,
    required this.entries,
  });

  final List<ProgressMetricModel> metrics;
  final List<ActivityEntryModel> entries;

  factory ProgressHistoryModel.fromJson(Map<String, dynamic> json) {
    final summary = json['summary'] as Map<String, dynamic>? ?? const {};
    final entries = (json['entries'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(ActivityEntryModel.fromJson)
        .toList();

    return ProgressHistoryModel(
      metrics: [
        ProgressMetricModel(
          label: 'Weight',
          value: summary['latestWeightLabel'] as String? ?? 'No data',
          trend: summary['lastUpdatedAt'] == null ? 'No update yet' : 'Latest check-in',
          accentColorValue: 0xFFF2A541,
        ),
        ProgressMetricModel(
          label: 'Check-ins',
          value: summary['checkInCountLabel'] as String? ?? '0',
          trend: 'Progress history',
          accentColorValue: 0xFF5DD6C0,
        ),
        ProgressMetricModel(
          label: 'Strength',
          value: summary['strengthLabel'] as String? ?? 'No notes',
          trend: summary['conditioningLabel'] as String? ?? '',
          accentColorValue: 0xFF8FA8FF,
        ),
      ],
      entries: entries,
    );
  }
}

class ProgressMetricModel {
  const ProgressMetricModel({
    required this.label,
    required this.value,
    required this.trend,
    required this.accentColorValue,
  });

  final String label;
  final String value;
  final String trend;
  final int accentColorValue;
}

class ActivityEntryModel {
  const ActivityEntryModel({
    required this.title,
    required this.subtitle,
    required this.whenLabel,
    required this.metric,
    this.positive = true,
  });

  final String title;
  final String subtitle;
  final String whenLabel;
  final String metric;
  final bool positive;

  factory ActivityEntryModel.fromJson(Map<String, dynamic> json) {
    final month = json['month'] as int? ?? 1;
    final year = json['year'] as int? ?? 2000;

    return ActivityEntryModel(
      title: json['title'] as String? ?? 'Progress update',
      subtitle: json['subtitle'] as String? ?? '',
      whenLabel: '${_monthName(month)} $year',
      metric: json['metric'] as String? ?? '',
      positive: json['positive'] as bool? ?? true,
    );
  }

  static String _monthName(int month) {
    const names = <String>[
      'Jan',
      'Feb',
      'Mar',
      'Apr',
      'May',
      'Jun',
      'Jul',
      'Aug',
      'Sep',
      'Oct',
      'Nov',
      'Dec',
    ];
    return names[(month - 1).clamp(0, 11)];
  }
}
