class TrainingSessionItem {
  const TrainingSessionItem({
    required this.id,
    required this.dayOfWeek,
    required this.dayName,
    required this.completedAt,
    required this.repetitions,
    this.note,
  });

  final int id;
  final int dayOfWeek;
  final String dayName;
  final String completedAt;
  final int repetitions;
  final String? note;

  factory TrainingSessionItem.fromJson(Map<String, dynamic> json) =>
      TrainingSessionItem(
        id: json['id'] as int? ?? 0,
        dayOfWeek: json['dayOfWeek'] as int? ?? 1,
        dayName: json['dayName']?.toString() ?? '',
        completedAt: json['completedAt']?.toString() ?? '',
        repetitions: json['repetitions'] as int? ?? 0,
        note: json['note']?.toString(),
      );
}
