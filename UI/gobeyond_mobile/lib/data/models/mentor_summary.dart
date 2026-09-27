import 'review.dart';

/// `MentorSummary` from the API contract, used for list cards, similar-mentor
/// carousels and the recommendation feed. `mentorProfileId` is an internal
/// key only (never rendered to the user).
class MentorSummary {
  MentorSummary({
    required this.mentorProfileId,
    required this.fullName,
    required this.trainingTypeId,
    required this.trainingTypeName,
    required this.averageRating,
    required this.reviewCount,
    required this.monthlyPrice,
    required this.currency,
    required this.yearsOfExperience,
    required this.age,
    this.nickname,
    this.profileImageUrl,
  });

  final int mentorProfileId;
  final String fullName;
  final String? nickname;
  final String? profileImageUrl;
  final int trainingTypeId;
  final String trainingTypeName;
  final double averageRating;
  final int reviewCount;
  final num monthlyPrice;
  final String currency;
  final int yearsOfExperience;
  final int age;

  factory MentorSummary.fromJson(Map<String, dynamic> json) => MentorSummary(
        mentorProfileId: json['mentorProfileId'] as int? ?? 0,
        fullName: json['fullName']?.toString() ?? '',
        nickname: json['nickname']?.toString(),
        profileImageUrl: json['profileImageUrl']?.toString(),
        trainingTypeId: json['trainingTypeId'] as int? ?? 0,
        trainingTypeName: json['trainingTypeName']?.toString() ?? '',
        averageRating: (json['averageRating'] as num?)?.toDouble() ?? 0,
        reviewCount: json['reviewCount'] as int? ?? 0,
        monthlyPrice: json['monthlyPrice'] as num? ?? 0,
        currency: json['currency']?.toString() ?? 'USD',
        yearsOfExperience: json['yearsOfExperience'] as int? ?? 0,
        age: json['age'] as int? ?? 0,
      );
}

class MentorDetail extends MentorSummary {
  MentorDetail({
    required super.mentorProfileId,
    required super.fullName,
    required super.trainingTypeId,
    required super.trainingTypeName,
    required super.averageRating,
    required super.reviewCount,
    required super.monthlyPrice,
    required super.currency,
    required super.yearsOfExperience,
    required super.age,
    required this.bio,
    required this.specializationNames,
    required this.reviews,
    super.nickname,
    super.profileImageUrl,
  });

  final String bio;
  final List<String> specializationNames;
  final List<Review> reviews;

  factory MentorDetail.fromJson(Map<String, dynamic> json) => MentorDetail(
        mentorProfileId: json['mentorProfileId'] as int? ?? 0,
        fullName: json['fullName']?.toString() ?? '',
        nickname: json['nickname']?.toString(),
        profileImageUrl: json['profileImageUrl']?.toString(),
        trainingTypeId: json['trainingTypeId'] as int? ?? 0,
        trainingTypeName: json['trainingTypeName']?.toString() ?? '',
        averageRating: (json['averageRating'] as num?)?.toDouble() ?? 0,
        reviewCount: json['reviewCount'] as int? ?? 0,
        monthlyPrice: json['monthlyPrice'] as num? ?? 0,
        currency: json['currency']?.toString() ?? 'USD',
        yearsOfExperience: json['yearsOfExperience'] as int? ?? 0,
        age: json['age'] as int? ?? 0,
        bio: json['bio']?.toString() ?? '',
        specializationNames:
            (json['specializationNames'] as List<dynamic>? ?? const [])
                .map((e) => e.toString())
                .toList(),
        reviews: (json['reviews'] as List<dynamic>? ?? const [])
            .whereType<Map<String, dynamic>>()
            .map(Review.fromJson)
            .toList(),
      );
}
