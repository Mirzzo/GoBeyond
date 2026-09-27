import 'mentor_summary.dart';

/// One row from `GET /api/recommendations/mentors` — a content-based
/// recommendation with a 0-1 score and human-readable reasons to show under
/// "PREPORUČENO ZA VAS" on the home screen.
class MentorRecommendation {
  const MentorRecommendation({
    required this.mentor,
    required this.score,
    required this.reasons,
  });

  final MentorSummary mentor;
  final double score;
  final List<String> reasons;

  factory MentorRecommendation.fromJson(Map<String, dynamic> json) =>
      MentorRecommendation(
        mentor: MentorSummary.fromJson(
            Map<String, dynamic>.from(json['mentor'] as Map)),
        score: (json['score'] as num?)?.toDouble() ?? 0,
        reasons: (json['reasons'] as List<dynamic>? ?? const [])
            .map((e) => e.toString())
            .toList(),
      );
}
