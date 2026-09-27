class ProgressEntryItem {
  const ProgressEntryItem({
    required this.id,
    required this.year,
    required this.month,
    required this.monthName,
    required this.weightKg,
    required this.measurements,
    required this.strength,
    required this.conditioning,
    required this.hasPlanSnapshot,
    this.photoUrl,
    this.createdAt,
    this.updatedAt,
  });

  final int id;
  final int year;
  final int month;
  final String monthName;
  final String? photoUrl;
  final num weightKg;
  final String measurements;
  final String strength;
  final String conditioning;
  final bool hasPlanSnapshot;
  final String? createdAt;
  final String? updatedAt;

  factory ProgressEntryItem.fromJson(Map<String, dynamic> json) =>
      ProgressEntryItem(
        id: json['id'] as int? ?? 0,
        year: json['year'] as int? ?? 0,
        month: json['month'] as int? ?? 1,
        monthName: json['monthName']?.toString() ?? '',
        photoUrl: json['photoUrl']?.toString(),
        weightKg: json['weightKg'] as num? ?? 0,
        measurements: json['measurements']?.toString() ?? '',
        strength: json['strength']?.toString() ?? '',
        conditioning: json['conditioning']?.toString() ?? '',
        hasPlanSnapshot: json['hasPlanSnapshot'] as bool? ?? false,
        createdAt: json['createdAt']?.toString(),
        updatedAt: json['updatedAt']?.toString(),
      );
}

class WeightPoint {
  const WeightPoint(
      {required this.year, required this.month, required this.weightKg});

  final int year;
  final int month;
  final num weightKg;

  factory WeightPoint.fromJson(Map<String, dynamic> json) => WeightPoint(
        year: json['year'] as int? ?? 0,
        month: json['month'] as int? ?? 1,
        weightKg: json['weightKg'] as num? ?? 0,
      );
}
