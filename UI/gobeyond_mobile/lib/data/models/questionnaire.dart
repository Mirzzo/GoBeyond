/// The 6 free-text KUPI PLAN questions (mockup 09), sent when creating a
/// subscription and echoed back on subscription detail.
class Questionnaire {
  const Questionnaire({
    required this.primaryGoal,
    required this.timeCommitment,
    required this.healthIssues,
    required this.medications,
    required this.weeklySessions,
    required this.outsideActivity,
  });

  final String primaryGoal;
  final String timeCommitment;
  final String healthIssues;
  final String medications;
  final String weeklySessions;
  final String outsideActivity;

  factory Questionnaire.fromJson(Map<String, dynamic> json) => Questionnaire(
        primaryGoal: json['primaryGoal']?.toString() ?? '',
        timeCommitment: json['timeCommitment']?.toString() ?? '',
        healthIssues: json['healthIssues']?.toString() ?? '',
        medications: json['medications']?.toString() ?? '',
        weeklySessions: json['weeklySessions']?.toString() ?? '',
        outsideActivity: json['outsideActivity']?.toString() ?? '',
      );

  Map<String, dynamic> toJson() => {
        'primaryGoal': primaryGoal,
        'timeCommitment': timeCommitment,
        'healthIssues': healthIssues,
        'medications': medications,
        'weeklySessions': weeklySessions,
        'outsideActivity': outsideActivity,
      };
}
