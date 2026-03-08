import 'app_role.dart';

class UserProfile {
  const UserProfile({
    required this.userId,
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.role,
    required this.isActive,
    this.profileImageUrl,
    this.mentorProfile,
    this.clientProfile,
  });

  final int userId;
  final String firstName;
  final String lastName;
  final String email;
  final AppRole role;
  final bool isActive;
  final String? profileImageUrl;
  final MentorProfileData? mentorProfile;
  final ClientProfileData? clientProfile;

  factory UserProfile.fromJson(Map<String, dynamic> json) {
    return UserProfile(
      userId: json['userId'] as int,
      firstName: json['firstName'] as String? ?? '',
      lastName: json['lastName'] as String? ?? '',
      email: json['email'] as String? ?? '',
      role: AppRoleParser.fromJsonValue(json['role']),
      isActive: json['isActive'] as bool? ?? true,
      profileImageUrl: json['profileImageUrl'] as String?,
      mentorProfile: json['mentorProfile'] is Map<String, dynamic>
          ? MentorProfileData.fromJson(json['mentorProfile'] as Map<String, dynamic>)
          : null,
      clientProfile: json['clientProfile'] is Map<String, dynamic>
          ? ClientProfileData.fromJson(json['clientProfile'] as Map<String, dynamic>)
          : null,
    );
  }

  String get fullName => '$firstName $lastName'.trim();
}

class MentorProfileData {
  const MentorProfileData({
    required this.bio,
    required this.age,
    required this.category,
    required this.price,
    required this.status,
    this.stripeAccountId,
  });

  final String bio;
  final int age;
  final String category;
  final double price;
  final String status;
  final String? stripeAccountId;

  factory MentorProfileData.fromJson(Map<String, dynamic> json) {
    return MentorProfileData(
      bio: json['bio'] as String? ?? '',
      age: json['age'] as int? ?? 0,
      category: json['category']?.toString() ?? 'Hybrid',
      price: (json['price'] as num?)?.toDouble() ?? 0,
      status: json['status']?.toString() ?? 'Pending',
      stripeAccountId: json['stripeAccountId'] as String?,
    );
  }
}

class ClientProfileData {
  const ClientProfileData({
    required this.weight,
    required this.height,
    required this.age,
    required this.fitnessLevel,
  });

  final double weight;
  final double height;
  final int age;
  final String fitnessLevel;

  factory ClientProfileData.fromJson(Map<String, dynamic> json) {
    return ClientProfileData(
      weight: (json['weight'] as num?)?.toDouble() ?? 0,
      height: (json['height'] as num?)?.toDouble() ?? 0,
      age: json['age'] as int? ?? 0,
      fitnessLevel: json['fitnessLevel'] as String? ?? '',
    );
  }
}

class UserProfileUpsertRequest {
  const UserProfileUpsertRequest({
    required this.firstName,
    required this.lastName,
    required this.email,
    this.profileImageUrl,
    this.mentorProfile,
    this.clientProfile,
  });

  final String firstName;
  final String lastName;
  final String email;
  final String? profileImageUrl;
  final UpsertMentorProfileData? mentorProfile;
  final UpsertClientProfileData? clientProfile;

  Map<String, dynamic> toJson() {
    return {
      'firstName': firstName,
      'lastName': lastName,
      'email': email,
      'profileImageUrl': profileImageUrl,
      'mentorProfile': mentorProfile?.toJson(),
      'clientProfile': clientProfile?.toJson(),
    };
  }
}

class UpsertMentorProfileData {
  const UpsertMentorProfileData({
    required this.bio,
    required this.age,
    required this.category,
    required this.price,
  });

  final String bio;
  final int age;
  final String category;
  final double price;

  Map<String, dynamic> toJson() {
    return {
      'bio': bio,
      'age': age,
      'category': category,
      'price': price,
    };
  }
}

class UpsertClientProfileData {
  const UpsertClientProfileData({
    required this.weight,
    required this.height,
    required this.age,
    required this.fitnessLevel,
  });

  final double weight;
  final double height;
  final int age;
  final String fitnessLevel;

  Map<String, dynamic> toJson() {
    return {
      'weight': weight,
      'height': height,
      'age': age,
      'fitnessLevel': fitnessLevel,
    };
  }
}
