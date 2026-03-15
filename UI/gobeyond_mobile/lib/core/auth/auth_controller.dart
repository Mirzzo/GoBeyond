import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../../data/repositories/auth_repository.dart';
import '../network/dio_client.dart';

class AuthController extends ChangeNotifier {
  AuthController({
    AuthRepository? repository,
    FlutterSecureStorage? storage,
  })  : _repository = repository ?? AuthRepository(DioClient()),
        _storage = storage ?? const FlutterSecureStorage();

  static const _accessTokenKey = 'access_token';
  static const _refreshTokenKey = 'refresh_token';
  static const _userKey = 'gb_user';
  static const _profileKey = 'gb_profile';

  final AuthRepository _repository;
  final FlutterSecureStorage _storage;

  bool _isHydrated = false;
  bool _isBusy = false;
  String? _accessToken;
  String? _refreshToken;
  Map<String, dynamic>? _user;
  Map<String, dynamic>? _profile;
  String? _errorMessage;

  bool get isHydrated => _isHydrated;
  bool get isBusy => _isBusy;
  bool get isAuthenticated => _accessToken != null && _user != null;
  Map<String, dynamic>? get user => _user;
  Map<String, dynamic>? get profile => _profile;
  String? get errorMessage => _errorMessage;

  Future<void> hydrate() async {
    _accessToken = await _storage.read(key: _accessTokenKey);
    _refreshToken = await _storage.read(key: _refreshTokenKey);

    final rawUser = await _storage.read(key: _userKey);
    if (rawUser != null && rawUser.isNotEmpty) {
      _user = Map<String, dynamic>.from(jsonDecode(rawUser) as Map<String, dynamic>);
    }

    final rawProfile = await _storage.read(key: _profileKey);
    if (rawProfile != null && rawProfile.isNotEmpty) {
      _profile = Map<String, dynamic>.from(jsonDecode(rawProfile) as Map<String, dynamic>);
    }

    if (_accessToken != null) {
      try {
        await refreshProfile();
      } catch (_) {
        await logout(notify: false);
      }
    }

    _isHydrated = true;
    notifyListeners();
  }

  Future<bool> login({
    required String email,
    required String password,
  }) async {
    _setBusy(true);
    _errorMessage = null;

    try {
      final response = await _repository.login(email, password);
      await _applyAuthResponse(response);
      return true;
    } catch (error) {
      _errorMessage = _extractMessage(error);
      notifyListeners();
      return false;
    } finally {
      _setBusy(false);
    }
  }

  Future<bool> registerClient(Map<String, dynamic> payload) async {
    _setBusy(true);
    _errorMessage = null;

    try {
      final response = await _repository.registerClient(payload);
      await _applyAuthResponse(response);
      return true;
    } catch (error) {
      _errorMessage = _extractMessage(error);
      notifyListeners();
      return false;
    } finally {
      _setBusy(false);
    }
  }

  Future<void> refreshProfile() async {
    final profile = await _repository.getMyProfile();
    _profile = profile;
    await _storage.write(key: _profileKey, value: jsonEncode(profile));
    notifyListeners();
  }

  Future<bool> updateProfile(Map<String, dynamic> payload) async {
    _setBusy(true);
    _errorMessage = null;

    try {
      final profile = await _repository.updateMyProfile(payload);
      _profile = profile;

      final firstName = profile['firstName']?.toString() ?? '';
      final lastName = profile['lastName']?.toString() ?? '';
      if (_user != null) {
        _user = {
          ..._user!,
          'name': '$firstName $lastName'.trim(),
          'email': profile['email'],
          'role': profile['role'],
        };
      }

      await _persistSession();
      notifyListeners();
      return true;
    } catch (error) {
      _errorMessage = _extractMessage(error);
      notifyListeners();
      return false;
    } finally {
      _setBusy(false);
    }
  }

  Future<void> logout({bool notify = true}) async {
    _accessToken = null;
    _refreshToken = null;
    _user = null;
    _profile = null;
    _errorMessage = null;

    await _storage.delete(key: _accessTokenKey);
    await _storage.delete(key: _refreshTokenKey);
    await _storage.delete(key: _userKey);
    await _storage.delete(key: _profileKey);

    if (notify) {
      notifyListeners();
    }
  }

  Future<void> _applyAuthResponse(Map<String, dynamic> response) async {
    _accessToken = response['accessToken']?.toString();
    _refreshToken = response['refreshToken']?.toString();
    _user = Map<String, dynamic>.from(response['user'] as Map<String, dynamic>);
    _profile = await _repository.getMyProfile();
    await _persistSession();
    notifyListeners();
  }

  Future<void> _persistSession() async {
    if (_accessToken == null || _refreshToken == null || _user == null) {
      return;
    }

    await _storage.write(key: _accessTokenKey, value: _accessToken);
    await _storage.write(key: _refreshTokenKey, value: _refreshToken);
    await _storage.write(key: _userKey, value: jsonEncode(_user));
    if (_profile != null) {
      await _storage.write(key: _profileKey, value: jsonEncode(_profile));
    }
  }

  void _setBusy(bool value) {
    _isBusy = value;
    notifyListeners();
  }

  String _extractMessage(Object error) {
    final raw = error.toString();
    if (raw.contains('message')) {
      return raw;
    }
    return 'Request failed.';
  }
}
