import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/services/message_service.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/api_error.dart';
import '../../../core/utils/formatters.dart';
import '../../widgets/avatar.dart';
import '../../widgets/dialogs.dart';
import '../../widgets/panel.dart';

/// PORUKE — thread list + chat, with a manual refresh button and periodic
/// background refresh while a conversation is open.
class MentorMessagesScreen extends StatefulWidget {
  const MentorMessagesScreen({super.key, this.initialSubscriptionId});

  final int? initialSubscriptionId;

  @override
  State<MentorMessagesScreen> createState() => _MentorMessagesScreenState();
}

class _MentorMessagesScreenState extends State<MentorMessagesScreen> {
  final _service = MessageService();
  final _searchController = TextEditingController();
  final _messageController = TextEditingController();
  Timer? _refreshTimer;

  bool _loadingThreads = true;
  bool _loadingMessages = false;
  bool _sending = false;
  List<Map<String, dynamic>> _threads = const [];
  List<Map<String, dynamic>> _messages = const [];
  int? _selectedSubscriptionId;
  String? _selectedName;

  // Monotonic request generations so a slow/older response (e.g. from the
  // 10s background poll) can never clobber a newer one that already landed
  // (e.g. after switching threads or re-searching).
  int _threadsRequestId = 0;
  int _messagesRequestId = 0;

  @override
  void initState() {
    super.initState();
    _selectedSubscriptionId = widget.initialSubscriptionId;
    _loadThreads();
    if (_selectedSubscriptionId != null) {
      _loadMessages(_selectedSubscriptionId!);
    }
    _refreshTimer = Timer.periodic(const Duration(seconds: 10), (_) {
      _loadThreads(silent: true);
      if (_selectedSubscriptionId != null) _loadMessages(_selectedSubscriptionId!, silent: true);
    });
  }

  @override
  void dispose() {
    _refreshTimer?.cancel();
    _searchController.dispose();
    _messageController.dispose();
    super.dispose();
  }

  Future<void> _loadThreads({bool silent = false}) async {
    final requestId = ++_threadsRequestId;
    if (!silent) setState(() => _loadingThreads = true);
    try {
      final threads = await _service.getThreads(search: _searchController.text);
      if (!mounted || requestId != _threadsRequestId) return;
      setState(() {
        _threads = threads;
        _loadingThreads = false;
        final selected = threads.where((t) => t['subscriptionId'] == _selectedSubscriptionId);
        if (selected.isNotEmpty) _selectedName = selected.first['otherPartyName'] as String?;
      });
    } catch (error) {
      if (!mounted || requestId != _threadsRequestId) return;
      setState(() => _loadingThreads = false);
      if (!silent) showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _loadMessages(int subscriptionId, {bool silent = false}) async {
    final requestId = ++_messagesRequestId;
    if (!silent) setState(() => _loadingMessages = true);
    try {
      final messages = await _service.getMessages(subscriptionId);
      if (!mounted || requestId != _messagesRequestId) return;
      setState(() {
        _messages = messages;
        _loadingMessages = false;
      });
    } catch (error) {
      if (!mounted || requestId != _messagesRequestId) return;
      setState(() => _loadingMessages = false);
      if (!silent) showErrorSnack(context, ApiError.from(error).message);
    }
  }

  void _selectThread(Map<String, dynamic> thread) {
    setState(() {
      _selectedSubscriptionId = thread['subscriptionId'] as int;
      _selectedName = thread['otherPartyName'] as String?;
    });
    _loadMessages(_selectedSubscriptionId!);
  }

  Future<void> _send() async {
    final content = _messageController.text.trim();
    if (content.isEmpty || content.length > 2000) {
      showErrorSnack(context, 'Poruka mora imati između 1 i 2000 znakova.');
      return;
    }
    final subscriptionId = _selectedSubscriptionId;
    if (subscriptionId == null) return;
    setState(() => _sending = true);
    try {
      await _service.sendMessage(subscriptionId, content);
      _messageController.clear();
      await _loadMessages(subscriptionId, silent: true);
      await _loadThreads(silent: true);
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return ContentPanel(
      title: 'PORUKE',
      headerActions: [
        IconButton(
          tooltip: 'Osvježi',
          icon: const Icon(Icons.refresh, color: Colors.white),
          onPressed: () {
            _loadThreads();
            if (_selectedSubscriptionId != null) _loadMessages(_selectedSubscriptionId!);
          },
        ),
      ],
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SizedBox(
            width: 300,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                SearchField(controller: _searchController, hintText: 'Pretraga razgovora', onSubmitted: (_) => _loadThreads()),
                const SizedBox(height: 12),
                Expanded(
                  child: _loadingThreads
                      ? const Center(child: CircularProgressIndicator())
                      : _threads.isEmpty
                          ? const EmptyState(message: 'Nema razgovora.')
                          : ListView.separated(
                              itemCount: _threads.length,
                              separatorBuilder: (_, _) => const SizedBox(height: 8),
                              itemBuilder: (context, index) {
                                final thread = _threads[index];
                                final selected = thread['subscriptionId'] == _selectedSubscriptionId;
                                final unread = thread['unreadCount'] as int? ?? 0;
                                return InkWell(
                                  borderRadius: BorderRadius.circular(12),
                                  onTap: () => _selectThread(thread),
                                  child: Container(
                                    padding: const EdgeInsets.all(12),
                                    decoration: BoxDecoration(
                                      color: selected ? AppColors.accent.withValues(alpha: 0.18) : AppColors.panelLight,
                                      borderRadius: BorderRadius.circular(12),
                                      border: selected ? Border.all(color: AppColors.accent) : null,
                                    ),
                                    child: Row(children: [
                                      GbAvatar(imageUrl: thread['otherPartyPhotoUrl'] as String?, size: 40),
                                      const SizedBox(width: 10),
                                      Expanded(
                                        child: Column(
                                          crossAxisAlignment: CrossAxisAlignment.start,
                                          children: [
                                            Text(thread['otherPartyName'] as String? ?? '', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 13)),
                                            Text(thread['lastMessage'] as String? ?? '', maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
                                          ],
                                        ),
                                      ),
                                      if (unread > 0)
                                        Container(
                                          padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
                                          decoration: const BoxDecoration(color: AppColors.danger, shape: BoxShape.circle),
                                          child: Text('$unread', style: const TextStyle(color: Colors.white, fontSize: 10)),
                                        ),
                                    ]),
                                  ),
                                );
                              },
                            ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 20),
          Expanded(child: _buildChatPanel()),
        ],
      ),
    );
  }

  Widget _buildChatPanel() {
    if (_selectedSubscriptionId == null) {
      return const EmptyState(message: 'Odaberite razgovor sa liste.');
    }
    final canSend = _threads.firstWhere(
          (t) => t['subscriptionId'] == _selectedSubscriptionId,
          orElse: () => const {'canSend': true},
        )['canSend'] !=
        false;

    return Container(
      decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(16)),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(_selectedName ?? '', style: const TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 16)),
          const Divider(color: Colors.white24),
          Expanded(
            child: _loadingMessages
                ? const Center(child: CircularProgressIndicator())
                : _messages.isEmpty
                    ? const EmptyState(message: 'Nema poruka. Napišite prvu poruku.')
                    : ListView.builder(
                        reverse: false,
                        itemCount: _messages.length,
                        itemBuilder: (context, index) {
                          final message = _messages[index];
                          final isMine = message['isMine'] == true;
                          return Align(
                            alignment: isMine ? Alignment.centerRight : Alignment.centerLeft,
                            child: Container(
                              margin: const EdgeInsets.symmetric(vertical: 4),
                              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                              constraints: const BoxConstraints(maxWidth: 420),
                              decoration: BoxDecoration(
                                color: isMine ? AppColors.accent : AppColors.panelDark,
                                borderRadius: BorderRadius.circular(14),
                              ),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(message['content'] as String? ?? '', style: TextStyle(color: isMine ? Colors.black : Colors.white)),
                                  const SizedBox(height: 4),
                                  Text(Formatters.dateTime(message['sentAt'] as String?),
                                      style: TextStyle(color: isMine ? Colors.black54 : AppColors.textMuted, fontSize: 10)),
                                ],
                              ),
                            ),
                          );
                        },
                      ),
          ),
          const SizedBox(height: 12),
          if (!canSend)
            const Text('Slanje poruka nije dozvoljeno za ovu pretplatu.', style: TextStyle(color: AppColors.textMuted))
          else
            Row(children: [
              Expanded(
                child: TextField(
                  controller: _messageController,
                  maxLength: 2000,
                  style: const TextStyle(color: Colors.white),
                  decoration: const InputDecoration(hintText: 'Napišite poruku...', counterText: ''),
                  onSubmitted: (_) => _send(),
                ),
              ),
              const SizedBox(width: 10),
              ElevatedButton(
                onPressed: _sending ? null : _send,
                child: _sending
                    ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.black))
                    : const Icon(Icons.send),
              ),
            ]),
        ],
      ),
    );
  }
}
