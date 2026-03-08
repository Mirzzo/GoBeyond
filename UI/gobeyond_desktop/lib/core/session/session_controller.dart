import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';

import '../models/auth_user.dart';
import '../models/user_profile.dart';
import '../network/api_client.dart';
import '../services/auth_api_service.dart';

class SessionController extends ChangeNotifier {
  SessionController()
      : _authApiService = AuthApiService(ApiClient());

  static const _tokenKey = 'gb_access_token';
  static const _userKey = 'gb_user';
  static const _sessionFileName = '.gobeyond_desktop_session.json';

  final AuthApiService _authApiService;

  String? _accessToken;
  AuthUser? _user;
  bool _isBusy = false;
  bool _isHydrated = false;
  String? _errorMessage;

  String? get accessToken => _accessToken;
  AuthUser? get user => _user;
  bool get isAuthenticated => _accessToken != null && _user != null;
  bool get isBusy => _isBusy;
  bool get isHydrated => _isHydrated;
  String? get errorMessage => _errorMessage;

  Future<void> hydrate() async {
    final file = await _sessionFile();
    if (await file.exists()) {
      final rawContent = await file.readAsString();
      if (rawContent.isNotEmpty) {
        final payload = jsonDecode(rawContent) as Map<String, dynamic>;
        _accessToken = payload[_tokenKey] as String?;

        final rawUser = payload[_userKey];
        if (rawUser is Map<String, dynamic>) {
          _user = AuthUser.fromJson(rawUser);
        }
      }
    }

    _isHydrated = true;
    notifyListeners();
  }

  Future<bool> login({required String email, required String password}) async {
    _setBusy(true);
    _errorMessage = null;

    try {
      final authResponse = await _authApiService.login(email: email, password: password);
      _accessToken = authResponse.accessToken;
      _user = authResponse.user;

      await _persistSession();

      notifyListeners();
      return true;
    } catch (_) {
      _errorMessage = 'Login failed.';
      notifyListeners();
      return false;
    } finally {
      _setBusy(false);
    }
  }

  Future<void> logout() async {
    _accessToken = null;
    _user = null;

    final file = await _sessionFile();
    if (await file.exists()) {
      await file.delete();
    }

    notifyListeners();
  }

  Future<void> syncUserFromProfile(UserProfile profile) async {
    if (_user == null) {
      return;
    }

    _user = _user!.copyWith(
      name: profile.fullName,
      email: profile.email,
      role: profile.role,
    );
    await _persistSession();
    notifyListeners();
  }

  void clearError() {
    _errorMessage = null;
    notifyListeners();
  }

  void _setBusy(bool value) {
    _isBusy = value;
    notifyListeners();
  }

  Future<void> _persistSession() async {
    if (_accessToken == null || _user == null) {
      return;
    }

    final file = await _sessionFile();
    final payload = {
      _tokenKey: _accessToken,
      _userKey: _user!.toJson(),
    };
    await file.writeAsString(jsonEncode(payload), flush: true);
  }

  Future<File> _sessionFile() async {
    final appDataPath = Platform.environment['APPDATA'] ?? Directory.current.path;
    final directory = Directory(appDataPath);
    if (!await directory.exists()) {
      await directory.create(recursive: true);
    }

    return File('${directory.path}\\$_sessionFileName');
  }
}
