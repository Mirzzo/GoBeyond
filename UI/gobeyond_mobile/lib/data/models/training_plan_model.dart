class TrainingPlanModel {
  TrainingPlanModel({
    required this.id,
    required this.motivationalQuote,
    required this.weekNumber,
    required this.focusTitle,
    required this.focusSummary,
    required this.completedSessions,
    required this.totalSessions,
    required this.nextSessionTitle,
    required this.nextSessionDuration,
    required this.days,
  });

  final int id;
  final String motivationalQuote;
  final int weekNumber;
  final String focusTitle;
  final String focusSummary;
  final int completedSessions;
  final int totalSessions;
  final String nextSessionTitle;
  final String nextSessionDuration;
  final List<PlanDayModel> days;

  double get completionRate {
    if (totalSessions == 0) {
      return 0;
    }

    return completedSessions / totalSessions;
  }

  factory TrainingPlanModel.fromJson(Map<String, dynamic> json) {
    final days = (json['days'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(PlanDayModel.fromJson)
        .toList();

    return TrainingPlanModel(
      id: json['id'] as int? ?? 0,
      motivationalQuote: json['motivationalQuote'] as String? ?? '',
      weekNumber: json['weekNumber'] as int? ?? 1,
      focusTitle: json['focusTitle'] as String? ?? 'Structured training week',
      focusSummary: json['focusSummary'] as String? ?? 'Daily structure ready for review.',
      completedSessions: json['completedSessions'] as int? ?? 0,
      totalSessions: json['totalSessions'] as int? ?? days.length,
      nextSessionTitle: json['nextSessionTitle'] as String? ?? 'Session ready',
      nextSessionDuration: json['nextSessionDuration'] as String? ?? '',
      days: days,
    );
  }
}

class PlanDayModel {
  PlanDayModel({
    required this.dayLabel,
    required this.title,
    required this.focus,
    required this.durationLabel,
    required this.summary,
    required this.mainBlocks,
    required this.coachNote,
    this.completed = false,
    this.recovery = false,
  });

  final String dayLabel;
  final String title;
  final String focus;
  final String durationLabel;
  final String summary;
  final List<String> mainBlocks;
  final String coachNote;
  final bool completed;
  final bool recovery;

  factory PlanDayModel.fromJson(Map<String, dynamic> json) {
    final description = json['trainingDescription'] as String? ?? '';
    final nutrition = json['nutritionDescription'] as String? ?? '';
    final blocks = description
        .split(RegExp(r'[,.]'))
        .map((part) => part.trim())
        .where((part) => part.isNotEmpty)
        .toList();
    final dayName = json['dayOfWeek'] as String? ?? 'Day';
    final recovery = description.toLowerCase().contains('recovery') ||
        description.toLowerCase().contains('walk') ||
        description.toLowerCase().contains('mobility');

    return PlanDayModel(
      dayLabel: dayName.substring(0, dayName.length < 3 ? dayName.length : 3),
      title: dayName,
      focus: recovery ? 'Recovery' : 'Training',
      durationLabel: json['trainingDuration'] as String? ?? '',
      summary: description,
      mainBlocks: blocks.isEmpty ? [description] : blocks,
      coachNote: nutrition,
      completed: false,
      recovery: recovery,
    );
  }
}
