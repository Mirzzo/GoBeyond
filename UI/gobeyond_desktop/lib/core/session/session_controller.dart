import 'dart:convert';
import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../models/auth_response.dart';
import '../models/auth_user.dart';
import '../models/user_profile.dart';
import '../network/api_client.dart';
import '../services/auth_api_service.dart';

class SessionController extends ChangeNotifier {
  SessionController()
      : _authApiService = AuthApiService(ApiClient());

  static const _tokenKey = 'gb_access_token';
  static const _refreshTokenKey = 'gb_refresh_token';
  static const _userKey = 'gb_user';
  static const _sessionFileName = '.gobeyond_desktop_session.json';

  final AuthApiService _authApiService;

  String? _accessToken;
  String? _refreshToken;
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
        _refreshToken = payload[_refreshTokenKey] as String?;

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
      _errorMessage = null;
      await _applyAuthResponse(authResponse);
      return true;
    } catch (error) {
      _errorMessage = _extractErrorMessage(error, fallback: 'Login failed.');
      notifyListeners();
      return false;
    } finally {
      _setBusy(false);
    }
  }

  Future<T> runAuthenticated<T>(Future<T> Function(String token) request) async {
    final accessToken = _accessToken;
    if (accessToken == null) {
      throw const AuthenticationRequiredException();
    }

    try {
      return await request(accessToken);
    } on DioException catch (error) {
      if (error.response?.statusCode != 401) {
        rethrow;
      }

      final refreshed = await _refreshSession();
      if (!refreshed) {
        await logout(message: const SessionExpiredException().message);
        throw const SessionExpiredException();
      }

      final retriedToken = _accessToken;
      if (retriedToken == null) {
        await logout(message: const SessionExpiredException().message);
        throw const SessionExpiredException();
      }

      return request(retriedToken);
    }
  }

  Future<void> logout({String? message}) async {
    _accessToken = null;
    _refreshToken = null;
    _user = null;
    _errorMessage = message;

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

  Future<bool> _refreshSession() async {
    final refreshToken = _refreshToken;
    if (refreshToken == null || refreshToken.isEmpty) {
      return false;
    }

    try {
      final authResponse = await _authApiService.refresh(refreshToken: refreshToken);
      _errorMessage = null;
      await _applyAuthResponse(authResponse, notify: false);
      return true;
    } catch (_) {
      return false;
    }
  }

  Future<void> _applyAuthResponse(AuthResponse authResponse, {bool notify = true}) async {
    _accessToken = authResponse.accessToken;
    _refreshToken = authResponse.refreshToken;
    _user = authResponse.user;

    await _persistSession();

    if (notify) {
      notifyListeners();
    }
  }

  Future<void> _persistSession() async {
    if (_accessToken == null || _refreshToken == null || _user == null) {
      return;
    }

    final file = await _sessionFile();
    final payload = {
      _tokenKey: _accessToken,
      _refreshTokenKey: _refreshToken,
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

  String _extractErrorMessage(Object error, {required String fallback}) {
    if (error is DioException) {
      final responseData = error.response?.data;
      if (responseData is Map<String, dynamic>) {
        final message = responseData['message'];
        if (message is String && message.trim().isNotEmpty) {
          return message;
        }
      }

      return error.message ?? fallback;
    }

    return fallback;
  }
}

class AuthenticationRequiredException implements Exception {
  const AuthenticationRequiredException([
    this.message = 'Please sign in to continue.',
  ]);

  final String message;

  @override
  String toString() => message;
}

class SessionExpiredException implements Exception {
  const SessionExpiredException([
    this.message = 'Your session expired. Please sign in again.',
  ]);

  final String message;

  @override
  String toString() => message;
}
