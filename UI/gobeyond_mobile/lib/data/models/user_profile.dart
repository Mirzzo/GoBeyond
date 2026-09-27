class ClientProfileInfo {
  const ClientProfileInfo({
    required this.weightKg,
    required this.heightCm,
    required this.fitnessLevelId,
    required this.fitnessLevelName,
    required this.trainingExperienceYears,
    required this.fitnessGoalId,
    required this.fitnessGoalName,
    this.goalDescription,
    this.preferredTrainingTypeId,
  });

  final num weightKg;
  final num heightCm;
  final int fitnessLevelId;
  final String fitnessLevelName;
  final int trainingExperienceYears;
  final int fitnessGoalId;
  final String fitnessGoalName;
  final String? goalDescription;
  final int? preferredTrainingTypeId;

  factory ClientProfileInfo.fromJson(Map<String, dynamic> json) =>
      ClientProfileInfo(
        weightKg: json['weightKg'] as num? ?? 0,
        heightCm: json['heightCm'] as num? ?? 0,
        fitnessLevelId: json['fitnessLevelId'] as int? ?? 0,
        fitnessLevelName: json['fitnessLevelName']?.toString() ?? '',
        trainingExperienceYears: json['trainingExperienceYears'] as int? ?? 0,
        fitnessGoalId: json['fitnessGoalId'] as int? ?? 0,
        fitnessGoalName: json['fitnessGoalName']?.toString() ?? '',
        goalDescription: json['goalDescription']?.toString(),
        preferredTrainingTypeId: json['preferredTrainingTypeId'] as int?,
      );
}

/// `UserProfile` from `GET/PUT /api/user-profile/me`. This mobile app only
/// ever deals with the Client role, so the mentor sub-object is ignored.
class UserProfile {
  const UserProfile({
    required this.username,
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.dateOfBirth,
    required this.genderId,
    required this.genderName,
    required this.role,
    this.phoneNumber,
    this.profileImageUrl,
    this.client,
  });

  final String username;
  final String firstName;
  final String lastName;
  final String email;
  final String? phoneNumber;
  final String dateOfBirth;
  final int genderId;
  final String genderName;
  final String role;
  final String? profileImageUrl;
  final ClientProfileInfo? client;

  String get fullName => '$firstName $lastName'.trim();

  factory UserProfile.fromJson(Map<String, dynamic> json) => UserProfile(
        username: json['username']?.toString() ?? '',
        firstName: json['firstName']?.toString() ?? '',
        lastName: json['lastName']?.toString() ?? '',
        email: json['email']?.toString() ?? '',
        phoneNumber: json['phoneNumber']?.toString(),
        dateOfBirth: json['dateOfBirth']?.toString() ?? '',
        genderId: json['genderId'] as int? ?? 0,
        genderName: json['genderName']?.toString() ?? '',
        role: json['role']?.toString() ?? 'Client',
        profileImageUrl: json['profileImageUrl']?.toString(),
        client: json['client'] != null
            ? ClientProfileInfo.fromJson(
                Map<String, dynamic>.from(json['client'] as Map))
            : null,
      );
}
