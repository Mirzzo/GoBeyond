class DayPlan {
  const DayPlan({
    required this.id,
    required this.dayOfWeek,
    required this.dayName,
    required this.trainingDurationMinutes,
    required this.trainingDescription,
    required this.nutritionDescription,
    this.nutritionDurationMinutes,
  });

  final int id;
  final int dayOfWeek;
  final String dayName;
  final int trainingDurationMinutes;
  final String trainingDescription;
  final int? nutritionDurationMinutes;
  final String nutritionDescription;

  factory DayPlan.fromJson(Map<String, dynamic> json) => DayPlan(
        id: json['id'] as int? ?? 0,
        dayOfWeek: json['dayOfWeek'] as int? ?? 1,
        dayName: json['dayName']?.toString() ?? '',
        trainingDurationMinutes: json['trainingDurationMinutes'] as int? ?? 0,
        trainingDescription: json['trainingDescription']?.toString() ?? '',
        nutritionDurationMinutes: json['nutritionDurationMinutes'] as int?,
        nutritionDescription: json['nutritionDescription']?.toString() ?? '',
      );
}

/// `PlanDetail` — the client's currently published/archived training plan.
class TrainingPlan {
  const TrainingPlan({
    required this.id,
    required this.subscriptionId,
    required this.mentorFullName,
    required this.clientFullName,
    required this.status,
    required this.version,
    required this.canEdit,
    required this.days,
    this.motivationalQuote,
  });

  final int id;
  final int subscriptionId;
  final String mentorFullName;
  final String clientFullName;
  final String? motivationalQuote;
  final String status;
  final int version;
  final bool canEdit;
  final List<DayPlan> days;

  DayPlan? dayFor(int dayOfWeek) {
    for (final day in days) {
      if (day.dayOfWeek == dayOfWeek) return day;
    }
    return null;
  }

  factory TrainingPlan.fromJson(Map<String, dynamic> json) => TrainingPlan(
        id: json['id'] as int? ?? 0,
        subscriptionId: json['subscriptionId'] as int? ?? 0,
        mentorFullName: json['mentorFullName']?.toString() ?? '',
        clientFullName: json['clientFullName']?.toString() ?? '',
        motivationalQuote: json['motivationalQuote']?.toString(),
        status: json['status']?.toString() ?? 'Draft',
        version: json['version'] as int? ?? 1,
        canEdit: json['canEdit'] as bool? ?? false,
        days: (json['days'] as List<dynamic>? ?? const [])
            .whereType<Map<String, dynamic>>()
            .map(DayPlan.fromJson)
            .toList(),
      );
}
