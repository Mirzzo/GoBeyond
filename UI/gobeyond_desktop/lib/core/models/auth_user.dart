import 'app_role.dart';

/// `AuthResponse.user` shape from api-contract.md section 2.
class AuthUser {
  const AuthUser({
    required this.id,
    required this.username,
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.role,
    this.profileImageUrl,
  });

  final int id;
  final String username;
  final String firstName;
  final String lastName;
  final String email;
  final AppRole role;
  final String? profileImageUrl;

  String get fullName => '$firstName $lastName'.trim();

  factory AuthUser.fromJson(Map<String, dynamic> json) {
    return AuthUser(
      id: json['id'] as int,
      username: json['username'] as String? ?? '',
      firstName: json['firstName'] as String? ?? '',
      lastName: json['lastName'] as String? ?? '',
      email: json['email'] as String? ?? '',
      role: AppRole.fromWire(json['role']),
      profileImageUrl: json['profileImageUrl'] as String?,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'username': username,
      'firstName': firstName,
      'lastName': lastName,
      'email': email,
      'role': role.wireValue,
      'profileImageUrl': profileImageUrl,
    };
  }

  AuthUser copyWith({String? firstName, String? lastName, String? email, String? profileImageUrl}) {
    return AuthUser(
      id: id,
      username: username,
      firstName: firstName ?? this.firstName,
      lastName: lastName ?? this.lastName,
      email: email ?? this.email,
      role: role,
      profileImageUrl: profileImageUrl ?? this.profileImageUrl,
    );
  }
}

class AuthResponse {
  const AuthResponse({
    required this.accessToken,
    required this.refreshToken,
    required this.expiresAt,
    required this.user,
  });

  final String accessToken;
  final String refreshToken;
  final String expiresAt;
  final AuthUser user;

  factory AuthResponse.fromJson(Map<String, dynamic> json) {
    return AuthResponse(
      accessToken: json['accessToken'] as String,
      refreshToken: json['refreshToken'] as String,
      expiresAt: json['expiresAt']?.toString() ?? '',
      user: AuthUser.fromJson(Map<String, dynamic>.from(json['user'] as Map)),
    );
  }
}
