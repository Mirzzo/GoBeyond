import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/services/notification_service.dart';
import '../../core/session/session_controller.dart';
import '../../core/theme/app_theme.dart';
import '../../core/utils/api_error.dart';
import '../../core/utils/formatters.dart';
import '../widgets/dialogs.dart';
import '../widgets/panel.dart';

/// Pushed screen (with Back) opened from the top-bar bell icon.
class NotificationsScreen extends StatefulWidget {
  const NotificationsScreen({super.key});

  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen> {
  final _service = NotificationService();
  final _searchController = TextEditingController();
  bool _loading = true;
  bool _unreadOnly = false;
  // The last search text the user actually submitted (Enter), not whatever
  // is currently typed — the background poll must not apply a half-typed
  // query the user never asked for.
  String _submittedSearch = '';
  List<Map<String, dynamic>> _items = const [];
  Timer? _refreshTimer;
  // Bumped on every _load() call so a response from a superseded request
  // (e.g. the 30s poll landing after a filter change) is ignored instead of
  // overwriting newer data on screen.
  int _requestId = 0;

  @override
  void initState() {
    super.initState();
    _load();
    // The screen is pushed with its own AppBar, so the top-bar bell badge
    // (which only polls every 30s while that shell is visible) is hidden
    // while this is open — poll here too so a notification that arrives
    // while the user is looking at this screen still shows up.
    _refreshTimer = Timer.periodic(const Duration(seconds: 30), (_) => _load(silent: true));
  }

  @override
  void dispose() {
    _refreshTimer?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  /// [silent] is used by the background timer: it keeps the current list on
  /// screen (no full-screen spinner) and does not surface a transient
  /// network error as a snackbar every 30s.
  Future<void> _load({bool silent = false}) async {
    if (!silent) setState(() => _loading = true);
    final requestId = ++_requestId;
    try {
      final items = await _service.getNotifications(
        unreadOnly: _unreadOnly ? true : null,
        search: _submittedSearch,
      );
      if (!mounted || requestId != _requestId) return;
      setState(() {
        _items = items;
        _loading = false;
      });
      await context.read<SessionController>().refreshUnreadCount();
    } catch (error) {
      if (!mounted || requestId != _requestId) return;
      setState(() => _loading = false);
      if (!silent) showErrorSnack(context, ApiError.from(error).message);
    }
  }

  void _submitSearch() {
    _submittedSearch = _searchController.text;
    _load();
  }

  Future<void> _markRead(Map<String, dynamic> item) async {
    if (item['isRead'] == true) return;
    try {
      await _service.markRead(item['id'] as int);
      if (!mounted) return;
      setState(() => item['isRead'] = true);
      await context.read<SessionController>().refreshUnreadCount();
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _markAllRead() async {
    try {
      await _service.markAllRead();
      if (!mounted) return;
      showSuccessSnack(context, 'Sve obavijesti su označene kao pročitane.');
      await context.read<SessionController>().refreshUnreadCount();
      _load();
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Obavijesti'),
        backgroundColor: AppColors.panel,
        actions: [
          IconButton(tooltip: 'Osvježi', icon: const Icon(Icons.refresh), onPressed: () => _load()),
          TextButton(onPressed: _markAllRead, child: const Text('Označi sve kao pročitano')),
          const SizedBox(width: 8),
        ],
      ),
      body: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(children: [
              Expanded(
                child: SearchField(
                  controller: _searchController,
                  hintText: 'Pretraga po naslovu ili sadržaju',
                  onSubmitted: (_) => _submitSearch(),
                ),
              ),
              const SizedBox(width: 12),
              FilterChip(
                label: const Text('Samo nepročitane'),
                selected: _unreadOnly,
                onSelected: (value) {
                  setState(() => _unreadOnly = value);
                  _load();
                },
              ),
            ]),
            const SizedBox(height: 16),
            Expanded(
              child: _loading
                  ? const Center(child: CircularProgressIndicator())
                  : _items.isEmpty
                      ? const EmptyState(message: 'Nema obavijesti.')
                      : ListView.separated(
                          itemCount: _items.length,
                          separatorBuilder: (_, _) => const SizedBox(height: 10),
                          itemBuilder: (context, index) {
                            final item = _items[index];
                            final isRead = item['isRead'] == true;
                            return Container(
                              padding: const EdgeInsets.all(16),
                              decoration: BoxDecoration(
                                color: isRead ? AppColors.panel : AppColors.panelLight,
                                borderRadius: BorderRadius.circular(14),
                                border: isRead ? null : Border.all(color: AppColors.accent.withValues(alpha: 0.5)),
                              ),
                              child: InkWell(
                                onTap: () => _markRead(item),
                                child: Row(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Icon(isRead ? Icons.notifications_none : Icons.notifications_active,
                                        color: isRead ? AppColors.textMuted : AppColors.accent),
                                    const SizedBox(width: 14),
                                    Expanded(
                                      child: Column(
                                        crossAxisAlignment: CrossAxisAlignment.start,
                                        children: [
                                          Text(item['title'] as String? ?? '',
                                              style: TextStyle(
                                                  color: Colors.white,
                                                  fontWeight: isRead ? FontWeight.normal : FontWeight.bold)),
                                          const SizedBox(height: 4),
                                          Text(item['body'] as String? ?? '',
                                              style: const TextStyle(color: AppColors.textMuted)),
                                          const SizedBox(height: 6),
                                          Text(Formatters.dateTime(item['createdAt'] as String?),
                                              style: const TextStyle(color: AppColors.textMuted, fontSize: 11)),
                                        ],
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            );
                          },
                        ),
            ),
          ],
        ),
      ),
    );
  }
}
