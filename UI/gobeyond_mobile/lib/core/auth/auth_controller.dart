import 'dart:async';

import 'package:flutter/material.dart';

import '../../data/models/user_profile.dart';
import '../../data/repositories/activity_repository.dart';
import '../../data/repositories/auth_repository.dart';
import '../../data/repositories/profile_repository.dart';
import '../../presentation/screens/auth/login_register_screen.dart';
import '../constants/app_constants.dart';
import '../navigation/app_navigator.dart';
import '../network/api_exception.dart';
import '../network/dio_client.dart';
import 'session_storage.dart';

/// Holds the current session (tokens, cached user + profile) and drives the
/// 60s activity heartbeat while the app is authenticated and in the
/// foreground. Login is restricted to the Client role on this app, per the
/// mobile scope of the project.
class AuthController extends ChangeNotifier {
  AuthController({
    AuthRepository? authRepository,
    ProfileRepository? profileRepository,
    ActivityRepository? activityRepository,
    SessionStorage? storage,
  })  : _authRepository = authRepository ?? ApiAuthRepository(),
        _profileRepository = profileRepository ?? ApiProfileRepository(),
        _activityRepository = activityRepository ?? ApiActivityRepository(),
        _storage = storage ?? SessionStorage() {
    DioClient().onSessionExpired = _onSessionExpired;
  }

  final AuthRepository _authRepository;
  final ProfileRepository _profileRepository;
  final ActivityRepository _activityRepository;
  final SessionStorage _storage;

  bool _isHydrated = false;
  bool _isBusy = false;
  String? _refreshToken;
  Map<String, dynamic>? _user;
  UserProfile? _profile;
  String? _profileLoadError;
  Timer? _heartbeatTimer;
  bool _appInForeground = true;

  bool get isHydrated => _isHydrated;
  bool get isBusy => _isBusy;
  bool get isAuthenticated => _user != null;
  Map<String, dynamic>? get user => _user;
  UserProfile? get profile => _profile;

  /// Set when the profile couldn't be loaded for a reason other than an
  /// expired session (e.g. no network) — screens that need [profile] should
  /// offer a retry via [retryLoadProfile] instead of spinning forever.
  String? get profileLoadError => _profileLoadError;

  Future<void> hydrate() async {
    final accessToken = await _storage.accessToken;
    _refreshToken = await _storage.refreshToken;
    _user = await _storage.readUser();

    if (accessToken != null && _user != null) {
      await _loadProfile();
    }

    _isHydrated = true;
    notifyListeners();
  }

  /// Retries fetching the profile after [hydrate] (or a prior retry) failed
  /// with a non-401 error, e.g. a dropped connection.
  Future<void> retryLoadProfile() async {
    if (!isAuthenticated) return;
    await _loadProfile();
    notifyListeners();
  }

  Future<void> _loadProfile() async {
    try {
      _profile = await _profileRepository.getMyProfile();
      _profileLoadError = null;
      _startHeartbeatIfNeeded();
    } catch (error) {
      final apiError = ApiException.from(error);
      if (apiError.statusCode == 401) {
        await _clearSession();
      } else {
        _profileLoadError = apiError.message;
      }
    }
  }

  /// Throws [ApiException] on failure (invalid credentials, wrong role, ...).
  Future<void> login({
    required String usernameOrEmail,
    required String password,
  }) async {
    _setBusy(true);
    try {
      final response = await _authRepository.login(usernameOrEmail, password);
      final user = Map<String, dynamic>.from(response['user'] as Map);

      if (user['role'] != 'Client') {
        final refreshToken = response['refreshToken']?.toString();
        if (refreshToken != null) {
          try {
            await _authRepository.logout(refreshToken);
          } catch (_) {
            // Best effort revoke; role check below still blocks local login.
          }
        }
        throw ApiException('Mobilna aplikacija je namijenjena klijentima.',
            statusCode: 403);
      }

      await _applyAuthResponse(response, user);
    } finally {
      _setBusy(false);
    }
  }

  /// Throws [ApiException] (with field errors) on failure.
  Future<void> registerClient(Map<String, dynamic> payload) async {
    _setBusy(true);
    try {
      final response = await _authRepository.registerClient(payload);
      final user = Map<String, dynamic>.from(response['user'] as Map);
      await _applyAuthResponse(response, user);
    } finally {
      _setBusy(false);
    }
  }

  Future<void> _applyAuthResponse(
      Map<String, dynamic> response, Map<String, dynamic> user) async {
    final accessToken = response['accessToken']?.toString() ?? '';
    final refreshToken = response['refreshToken']?.toString() ?? '';
    await _storage.saveTokens(
        accessToken: accessToken, refreshToken: refreshToken);
    await _storage.saveUser(user);
    _refreshToken = refreshToken;
    _user = user;
    _profile = await _profileRepository.getMyProfile();
    _profileLoadError = null;
    _startHeartbeatIfNeeded();
    notifyListeners();
  }

  Future<void> refreshProfile() async {
    _profile = await _profileRepository.getMyProfile();
    notifyListeners();
  }

  /// Throws [ApiException] (with field errors) on failure.
  Future<void> updateProfile(Map<String, dynamic> payload) async {
    _setBusy(true);
    try {
      _profile = await _profileRepository.updateMyProfile(payload);
      if (_user != null) {
        _user = {
          ..._user!,
          'firstName': _profile!.firstName,
          'lastName': _profile!.lastName,
          'email': _profile!.email,
        };
        await _storage.saveUser(_user!);
      }
      notifyListeners();
    } finally {
      _setBusy(false);
    }
  }

  void setProfileImage(String? url) {
    if (_profile == null) return;
    _profile = UserProfile(
      username: _profile!.username,
      firstName: _profile!.firstName,
      lastName: _profile!.lastName,
      email: _profile!.email,
      phoneNumber: _profile!.phoneNumber,
      dateOfBirth: _profile!.dateOfBirth,
      genderId: _profile!.genderId,
      genderName: _profile!.genderName,
      role: _profile!.role,
      profileImageUrl: url,
      client: _profile!.client,
    );
    notifyListeners();
  }

  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmPassword,
  }) {
    return _authRepository.changePassword(
      currentPassword: currentPassword,
      newPassword: newPassword,
      confirmPassword: confirmPassword,
    );
  }

  Future<void> logout() async {
    _stopHeartbeat();
    final refreshToken = _refreshToken;
    if (refreshToken != null) {
      try {
        await _authRepository.logout(refreshToken);
      } catch (_) {
        // Best effort: still clear local session below.
      }
    }
    await _clearSession();
  }

  Future<void> _onSessionExpired() async {
    _stopHeartbeat();
    await _clearSession();
    // No BuildContext is available from a Dio interceptor: fall back to the
    // global navigator to reset the whole stack back to the login screen.
    AppNavigator.state?.pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const LoginRegisterScreen()),
      (route) => false,
    );
  }

  Future<void> _clearSession() async {
    await _storage.clear();
    _refreshToken = null;
    _user = null;
    _profile = null;
    _profileLoadError = null;
    notifyListeners();
  }

  void _setBusy(bool value) {
    _isBusy = value;
    notifyListeners();
  }

  void handleLifecycleChange(AppLifecycleState state) {
    _appInForeground = state == AppLifecycleState.resumed;
    if (_appInForeground) {
      _startHeartbeatIfNeeded();
    } else {
      _stopHeartbeat();
    }
  }

  void _startHeartbeatIfNeeded() {
    if (!isAuthenticated || !_appInForeground || _heartbeatTimer != null) {
      return;
    }
    _sendHeartbeat();
    _heartbeatTimer =
        Timer.periodic(AppConstants.heartbeatInterval, (_) => _sendHeartbeat());
  }

  void _stopHeartbeat() {
    _heartbeatTimer?.cancel();
    _heartbeatTimer = null;
  }

  Future<void> _sendHeartbeat() async {
    try {
      await _activityRepository.sendHeartbeat();
    } catch (_) {
      // Heartbeat failures are non-critical and silently ignored.
    }
  }

  @override
  void dispose() {
    _stopHeartbeat();
    super.dispose();
  }
}
