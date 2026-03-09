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

  double get completionRate {
    if (totalSessions == 0) {
      return 0;
    }

    return completedSessions / totalSessions;
  }
}
