import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/notification_item.dart';
import '../../../data/repositories/notification_repository.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/gb_scaffold.dart';
import '../../widgets/state_views.dart';

/// Obavijesti: search, unread styling, mark one / mark all read.
class NotificationsScreen extends StatefulWidget {
  const NotificationsScreen({super.key, this.notificationRepository});

  final NotificationRepository? notificationRepository;

  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen> {
  late final NotificationRepository _repository =
      widget.notificationRepository ?? ApiNotificationRepository();
  final _searchController = TextEditingController();
  Timer? _debounce;

  late Future<List<NotificationItem>> _future;

  @override
  void initState() {
    super.initState();
    _future = _repository.getNotifications();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  void _reload() => setState(() {
        _future = _repository.getNotifications(search: _searchController.text);
      });

  Future<void> _refresh() async {
    final next = _repository.getNotifications(search: _searchController.text);
    setState(() {
      _future = next;
    });
    try {
      await next;
    } catch (_) {
      // Surfaced via the FutureBuilder's own error state instead.
    }
  }

  void _onSearchChanged(String _) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 350), _reload);
  }

  Future<void> _markRead(NotificationItem item) async {
    if (item.isRead) return;
    try {
      await _repository.markRead(item.id);
      if (!mounted) return;
      _reload();
    } catch (_) {
      // Non-critical: leave item as-is on failure.
    }
  }

  Future<void> _markAllRead() async {
    try {
      await _repository.markAllRead();
      if (!mounted) return;
      _reload();
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(ApiException.from(error).message)));
    }
  }

  @override
  Widget build(BuildContext context) {
    return GbScaffold(
      title: 'Obavijesti',
      actions: [
        TextButton(
          onPressed: _markAllRead,
          child: const Text('Označi sve'),
        ),
      ],
      body: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text('OBAVIJESTI',
                style: TextStyle(fontWeight: FontWeight.w800, fontSize: 18)),
            const SizedBox(height: 14),
            TextField(
              controller: _searchController,
              onChanged: _onSearchChanged,
              decoration: const InputDecoration(
                hintText: 'PRETRAŽI OBAVIJESTI',
                suffixIcon: Icon(Icons.search_rounded),
              ),
            ),
            const SizedBox(height: 14),
            Expanded(
              child: RefreshIndicator(
                onRefresh: _refresh,
                child: FutureBuilder<List<NotificationItem>>(
                  future: _future,
                  builder: (context, snapshot) {
                    // A refresh keeps the loaded list on screen
                    // (FutureBuilder carries the previous data over while
                    // the new future runs).
                    if (!snapshot.hasData &&
                        snapshot.connectionState != ConnectionState.done) {
                      return const PullToRefreshFallback(child: LoadingView());
                    }
                    if (snapshot.hasError) {
                      return PullToRefreshFallback(
                        child: ErrorView(
                          message: ApiException.from(snapshot.error!).message,
                          onRetry: _reload,
                        ),
                      );
                    }
                    final notifications = snapshot.data!;
                    if (notifications.isEmpty) {
                      final searching =
                          _searchController.text.trim().isNotEmpty;
                      return PullToRefreshFallback(
                        child: EmptyStateView(
                          message: searching
                              ? 'Nema obavijesti za zadanu pretragu.'
                              : 'Nemate obavijesti.',
                          icon: searching
                              ? Icons.search_off_rounded
                              : Icons.notifications_none_rounded,
                        ),
                      );
                    }
                    return ListView.separated(
                      physics: const AlwaysScrollableScrollPhysics(),
                      itemCount: notifications.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 12),
                      itemBuilder: (context, index) {
                        final item = notifications[index];
                        return AppPanel(
                          color: item.isRead
                              ? AppTheme.panel
                              : AppTheme.panelLight,
                          onTap: () => _markRead(item),
                          child: Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              if (!item.isRead)
                                Container(
                                  margin:
                                      const EdgeInsets.only(top: 5, right: 10),
                                  width: 10,
                                  height: 10,
                                  decoration: const BoxDecoration(
                                    color: AppTheme.accent,
                                    shape: BoxShape.circle,
                                  ),
                                ),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(item.title,
                                        style: TextStyle(
                                          fontWeight: item.isRead
                                              ? FontWeight.w600
                                              : FontWeight.w800,
                                        )),
                                    const SizedBox(height: 4),
                                    Text(item.body,
                                        style: const TextStyle(fontSize: 13.5)),
                                    const SizedBox(height: 6),
                                    Text(
                                        Formatters.dateTimeLabel(
                                            item.createdAt),
                                        style: const TextStyle(
                                            color: AppTheme.textMuted,
                                            fontSize: 11.5)),
                                  ],
                                ),
                              ),
                            ],
                          ),
                        );
                      },
                    );
                  },
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
