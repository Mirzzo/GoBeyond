import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/message_thread.dart';
import '../../../data/repositories/message_repository.dart';
import '../../widgets/app_network_image.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/gb_scaffold.dart';
import '../../widgets/state_views.dart';
import 'message_chat_screen.dart';

/// Poruke: thread list with search, unread counts.
class MessageThreadListScreen extends StatefulWidget {
  const MessageThreadListScreen({super.key, this.messageRepository});

  final MessageRepository? messageRepository;

  @override
  State<MessageThreadListScreen> createState() =>
      _MessageThreadListScreenState();
}

class _MessageThreadListScreenState extends State<MessageThreadListScreen> {
  late final MessageRepository _repository =
      widget.messageRepository ?? ApiMessageRepository();
  final _searchController = TextEditingController();
  Timer? _debounce;

  late Future<List<MessageThread>> _future;

  @override
  void initState() {
    super.initState();
    _future = _repository.getThreads();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  void _reload() => setState(() {
        _future = _repository.getThreads(search: _searchController.text);
      });

  void _onSearchChanged(String _) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 350), _reload);
  }

  @override
  Widget build(BuildContext context) {
    return GbScaffold(
      title: 'Poruke',
      body: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text('PORUKE',
                style: TextStyle(fontWeight: FontWeight.w800, fontSize: 18)),
            const SizedBox(height: 14),
            TextField(
              controller: _searchController,
              onChanged: _onSearchChanged,
              decoration: const InputDecoration(
                hintText: 'PRETRAŽI PORUKE',
                suffixIcon: Icon(Icons.search_rounded),
              ),
            ),
            const SizedBox(height: 14),
            Expanded(
              child: FutureBuilder<List<MessageThread>>(
                future: _future,
                builder: (context, snapshot) {
                  if (snapshot.connectionState != ConnectionState.done) {
                    return const LoadingView();
                  }
                  if (snapshot.hasError) {
                    return ErrorView(
                      message: ApiException.from(snapshot.error!).message,
                      onRetry: _reload,
                    );
                  }
                  final threads = snapshot.data!;
                  if (threads.isEmpty) {
                    return const EmptyStateView(
                      message: 'Nemate nijednu konverzaciju.',
                      icon: Icons.chat_bubble_outline_rounded,
                    );
                  }
                  return ListView.separated(
                    itemCount: threads.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 12),
                    itemBuilder: (context, index) {
                      final thread = threads[index];
                      return AppPanel(
                        onTap: () async {
                          await Navigator.of(context).push(
                            MaterialPageRoute(
                              builder: (_) => MessageChatScreen(
                                subscriptionId: thread.subscriptionId,
                                otherPartyName: thread.otherPartyName,
                                otherPartyPhotoUrl: thread.otherPartyPhotoUrl,
                                canSend: thread.canSend,
                              ),
                            ),
                          );
                          if (!mounted) return;
                          _reload();
                        },
                        child: Row(
                          children: [
                            AppNetworkImage(
                              url: thread.otherPartyPhotoUrl,
                              width: 52,
                              height: 52,
                              borderRadius: 26,
                            ),
                            const SizedBox(width: 14),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(thread.otherPartyName,
                                      style: const TextStyle(
                                          fontWeight: FontWeight.w700)),
                                  if (thread.lastMessage != null) ...[
                                    const SizedBox(height: 3),
                                    Text(
                                      thread.lastMessage!,
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                      style: const TextStyle(
                                          color: AppTheme.textMuted,
                                          fontSize: 13),
                                    ),
                                  ],
                                ],
                              ),
                            ),
                            if (thread.lastMessageAt != null)
                              Padding(
                                padding: const EdgeInsets.only(right: 8),
                                child: Text(
                                  Formatters.dateTimeLabel(
                                      thread.lastMessageAt),
                                  style: const TextStyle(
                                      color: AppTheme.textMuted,
                                      fontSize: 10.5),
                                ),
                              ),
                            if (thread.unreadCount > 0)
                              Container(
                                padding: const EdgeInsets.symmetric(
                                    horizontal: 8, vertical: 4),
                                decoration: BoxDecoration(
                                  color: AppTheme.accent,
                                  borderRadius: BorderRadius.circular(12),
                                ),
                                child: Text(
                                  '${thread.unreadCount}',
                                  style: const TextStyle(
                                      color: AppTheme.onAccent,
                                      fontWeight: FontWeight.w800,
                                      fontSize: 11),
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
          ],
        ),
      ),
    );
  }
}
