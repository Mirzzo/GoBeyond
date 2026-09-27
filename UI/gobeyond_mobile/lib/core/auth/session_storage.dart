import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Single place that owns the persisted auth session (tokens + cached user)
/// in `flutter_secure_storage`, shared between [DioClient] (token attach /
/// refresh) and `AuthController` (login state) so there is exactly one
/// source of truth for where session data lives.
class SessionStorage {
  factory SessionStorage() => instance;

  SessionStorage._internal();

  static final SessionStorage instance = SessionStorage._internal();

  static const _accessTokenKey = 'gb_access_token';
  static const _refreshTokenKey = 'gb_refresh_token';
  static const _userKey = 'gb_user';

  final FlutterSecureStorage _storage = const FlutterSecureStorage();

  Future<String?> get accessToken => _storage.read(key: _accessTokenKey);

  Future<String?> get refreshToken => _storage.read(key: _refreshTokenKey);

  Future<void> saveTokens({
    required String accessToken,
    required String refreshToken,
  }) async {
    await _storage.write(key: _accessTokenKey, value: accessToken);
    await _storage.write(key: _refreshTokenKey, value: refreshToken);
  }

  Future<void> saveUser(Map<String, dynamic> user) =>
      _storage.write(key: _userKey, value: jsonEncode(user));

  Future<Map<String, dynamic>?> readUser() async {
    final raw = await _storage.read(key: _userKey);
    if (raw == null || raw.isEmpty) return null;
    return Map<String, dynamic>.from(jsonDecode(raw) as Map);
  }

  Future<void> clear() async {
    await _storage.delete(key: _accessTokenKey);
    await _storage.delete(key: _refreshTokenKey);
    await _storage.delete(key: _userKey);
  }
}
