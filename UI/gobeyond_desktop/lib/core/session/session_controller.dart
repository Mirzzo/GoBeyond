import 'dart:async';
import 'dart:convert';

import 'package:flutter/widgets.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../models/app_role.dart';
import '../models/auth_user.dart';
import '../network/api_client.dart';
import '../services/auth_service.dart';
import '../services/notification_service.dart';
import '../utils/api_error.dart';

/// Holds the authenticated session (tokens + user), drives the 60s activity
/// heartbeat (api-contract.md section 8), keeps the notification-bell unread
/// count fresh, and wires up the 401-refresh callback consumed by [ApiClient].
///
/// Tokens are kept in the OS credential store via [FlutterSecureStorage]
/// (Windows Credential Locker) rather than a plaintext file, and a refresh
/// failure (rotated/revoked refresh token — api-contract.md Changelog v1.1)
/// always fully clears the session so the app falls back to [LoginScreen]
/// instead of leaving a half-authenticated shell on screen.
class SessionController extends ChangeNotifier with WidgetsBindingObserver {
  SessionController() {
    WidgetsBinding.instance.addObserver(this);
    ApiClient.instance.onUnauthorized = _tryRefresh;
  }

  static const _sessionStorageKey = 'gb_session';

  final AuthService _authService = AuthService();
  final NotificationService _notificationService = NotificationService();
  final FlutterSecureStorage _secureStorage = const FlutterSecureStorage();

  Timer? _heartbeatTimer;
  Timer? _unreadPollTimer;
  bool _appActive = true;

  String? _accessToken;
  String? _refreshToken;
  AuthUser? _user;
  bool _isBusy = false;
  bool _isHydrated = false;
  String? _errorMessage;
  int _unreadCount = 0;

  AuthUser? get user => _user;
  bool get isAuthenticated => _accessToken != null && _user != null;
  bool get isAdmin => _user?.role == AppRole.admin;
  bool get isMentor => _user?.role == AppRole.mentor;
  bool get isBusy => _isBusy;
  bool get isHydrated => _isHydrated;
  String? get errorMessage => _errorMessage;
  int get unreadCount => _unreadCount;

  Future<void> hydrate() async {
    try {
      final raw = await _secureStorage.read(key: _sessionStorageKey);
      if (raw != null && raw.isNotEmpty) {
        final payload = jsonDecode(raw) as Map<String, dynamic>;
        _refreshToken = payload['refreshToken'] as String?;
        if (_refreshToken != null) {
          // On failure this already clears local state via _tryRefresh
          // (silently — the session was never "live" in the UI yet).
          await _tryRefresh();
        }
      }
    } catch (_) {
      _accessToken = null;
      _refreshToken = null;
      _user = null;
    }

    _isHydrated = true;
    notifyListeners();
  }

  Future<bool> login({required String username, required String password}) async {
    _setBusy(true);
    _errorMessage = null;
    try {
      final auth = await _authService.login(username: username, password: password);
      if (auth.user.role == AppRole.client) {
        _errorMessage = 'Desktop aplikacija je namijenjena administratorima i mentorima.';
        return false;
      }
      await _applyAuthResponse(auth);
      return true;
    } catch (error) {
      _errorMessage = ApiError.from(error, fallback: 'Prijava nije uspjela. Pokušajte ponovo.').message;
      return false;
    } finally {
      _setBusy(false);
      notifyListeners();
    }
  }

  Future<void> logout() async {
    final refreshToken = _refreshToken;
    try {
      if (refreshToken != null) {
        await _authService.logout(refreshToken: refreshToken);
      }
    } catch (_) {
      // Best-effort; still clear the local session below.
    }
    await _clearLocalSession();
  }

  void updateUser(AuthUser updated) {
    _user = updated;
    unawaited(_persistSession());
    notifyListeners();
  }

  Future<void> refreshUnreadCount() async {
    if (!isAuthenticated) return;
    try {
      _unreadCount = await _notificationService.unreadCount();
      notifyListeners();
    } catch (_) {
      // Ignore transient failures; badge just won't update this tick.
    }
  }

  void clearError() {
    _errorMessage = null;
    notifyListeners();
  }

  void _setBusy(bool value) {
    _isBusy = value;
    notifyListeners();
  }

  Future<bool> _tryRefresh() async {
    final refreshToken = _refreshToken;
    if (refreshToken == null || refreshToken.isEmpty) return false;

    // Only a mid-session failure (the shell was already showing) should
    // surface a "session expired" message and force an immediate redirect;
    // a failure during the initial `hydrate()` should just quietly fall
    // back to the login screen.
    final wasAuthenticated = isAuthenticated;
    try {
      final auth = await _authService.refresh(refreshToken: refreshToken);
      await _applyAuthResponse(auth, notify: false);
      return true;
    } catch (_) {
      await _clearLocalSession(
        message: wasAuthenticated ? 'Sesija je istekla. Prijavite se ponovo.' : null,
        notify: wasAuthenticated,
      );
      return false;
    }
  }

  /// Cancels timers, wipes in-memory tokens/user, clears the API client's
  /// bearer token and deletes the persisted session — used by both explicit
  /// logout and a failed token refresh (rotated/revoked refresh token, or an
  /// access token invalidated by a block/role-change/password reset
  /// elsewhere — api-contract.md Changelog v1.1).
  Future<void> _clearLocalSession({String? message, bool notify = true}) async {
    _heartbeatTimer?.cancel();
    _unreadPollTimer?.cancel();
    _accessToken = null;
    _refreshToken = null;
    _user = null;
    _unreadCount = 0;
    _errorMessage = message;
    ApiClient.instance.setAccessToken(null);
    await _deletePersistedSession();
    if (notify) notifyListeners();
  }

  Future<void> _applyAuthResponse(AuthResponse auth, {bool notify = true}) async {
    _accessToken = auth.accessToken;
    _refreshToken = auth.refreshToken;
    _user = auth.user;
    ApiClient.instance.setAccessToken(_accessToken);
    await _persistSession();
    _startHeartbeat();
    _startUnreadPolling();
    unawaited(refreshUnreadCount());
    if (notify) notifyListeners();
  }

  void _startHeartbeat() {
    _heartbeatTimer?.cancel();
    _heartbeatTimer = Timer.periodic(const Duration(seconds: 60), (_) async {
      if (!_appActive || !isAuthenticated) return;
      try {
        await ApiClient.instance.dio.post<void>('/api/activity/heartbeat');
      } catch (_) {
        // Heartbeat failures must never interrupt the user's work.
      }
    });
  }

  void _startUnreadPolling() {
    _unreadPollTimer?.cancel();
    _unreadPollTimer = Timer.periodic(const Duration(seconds: 30), (_) => refreshUnreadCount());
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    _appActive = state == AppLifecycleState.resumed;
  }

  @override
  void dispose() {
    _heartbeatTimer?.cancel();
    _unreadPollTimer?.cancel();
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  Future<void> _persistSession() async {
    if (_accessToken == null || _refreshToken == null || _user == null) return;
    final payload = {
      'accessToken': _accessToken,
      'refreshToken': _refreshToken,
      'user': _user!.toJson(),
    };
    await _secureStorage.write(key: _sessionStorageKey, value: jsonEncode(payload));
  }

  Future<void> _deletePersistedSession() async {
    try {
      await _secureStorage.delete(key: _sessionStorageKey);
    } catch (_) {
      // Nothing more we can do if the OS credential store is unavailable.
    }
  }
}
