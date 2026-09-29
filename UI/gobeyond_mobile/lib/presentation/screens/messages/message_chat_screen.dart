import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/utils/validators.dart';
import '../../../data/models/message_thread.dart';
import '../../../data/repositories/message_repository.dart';
import '../../widgets/app_modal_page.dart';
import '../../widgets/state_views.dart';

/// PORUKA MENTORU chat view: message history + a validated send box.
class MessageChatScreen extends StatefulWidget {
  const MessageChatScreen({
    super.key,
    required this.subscriptionId,
    required this.otherPartyName,
    this.otherPartyPhotoUrl,
    this.canSend = true,
    this.messageRepository,
  });

  final int subscriptionId;
  final String otherPartyName;
  final String? otherPartyPhotoUrl;
  final bool canSend;
  final MessageRepository? messageRepository;

  @override
  State<MessageChatScreen> createState() => _MessageChatScreenState();
}

class _MessageChatScreenState extends State<MessageChatScreen>
    with WidgetsBindingObserver {
  late final MessageRepository _repository =
      widget.messageRepository ?? ApiMessageRepository();
  final _formKey = GlobalKey<FormState>();
  final _messageController = TextEditingController();
  bool _sending = false;
  String? _error;

  bool _loading = true;
  String? _loadError;
  List<MessageItem> _messages = const [];
  Timer? _pollTimer;

  // Every fetch (load or poll) gets an increasing id; a response older than
  // the last one applied is dropped, so a slow poll that started before a
  // send cannot replace the list that already contains the sent message.
  int _lastRequestId = 0;
  int _lastAppliedId = 0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _load();
    final lifecycle = WidgetsBinding.instance.lifecycleState;
    if (lifecycle == null || lifecycle == AppLifecycleState.resumed) {
      _startPolling();
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _stopPolling();
    _messageController.dispose();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      _startPolling();
      _poll();
    } else {
      // Opening the thread marks its messages read, so it must not be
      // fetched while the user cannot see the screen.
      _stopPolling();
    }
  }

  // Silent poll so a message the other party sends while this chat stays
  // open appears without the user having to leave and come back.
  void _startPolling() {
    _pollTimer ??= Timer.periodic(const Duration(seconds: 10), (_) => _poll());
  }

  void _stopPolling() {
    _pollTimer?.cancel();
    _pollTimer = null;
  }

  Future<void> _load() async {
    final requestId = ++_lastRequestId;
    setState(() {
      // Only the first load and a retry after an error show the spinner; a
      // reload after sending or a pull-to-refresh keeps the current list.
      if (_loadError != null) _loading = true;
      _loadError = null;
    });
    try {
      final messages =
          await _repository.getThreadMessages(widget.subscriptionId);
      if (!mounted) return;
      if (requestId < _lastAppliedId) {
        if (_loading) setState(() => _loading = false);
        return;
      }
      _lastAppliedId = requestId;
      setState(() {
        _messages = messages;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      if (requestId < _lastAppliedId) {
        if (_loading) setState(() => _loading = false);
        return;
      }
      setState(() {
        _loadError = ApiException.from(error).message;
        _loading = false;
      });
    }
  }

  Future<void> _poll() async {
    if (!mounted || _loading || _loadError != null) return;
    final requestId = ++_lastRequestId;
    try {
      final messages =
          await _repository.getThreadMessages(widget.subscriptionId);
      if (!mounted || requestId < _lastAppliedId) return;
      _lastAppliedId = requestId;
      if (!_sameMessages(messages, _messages)) {
        setState(() => _messages = messages);
      }
    } catch (_) {
      // Silent: a background poll failure shouldn't interrupt an open chat.
    }
  }

  bool _sameMessages(List<MessageItem> a, List<MessageItem> b) {
    if (a.length != b.length) return false;
    for (var i = 0; i < a.length; i++) {
      if (a[i].id != b[i].id || a[i].content != b[i].content) return false;
    }
    return true;
  }

  Future<void> _send() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    setState(() {
      _sending = true;
      _error = null;
    });
    try {
      await _repository.sendMessage(
          widget.subscriptionId, _messageController.text.trim());
      if (!mounted) return;
      _messageController.clear();
      await _load();
    } catch (error) {
      if (!mounted) return;
      setState(() => _error = ApiException.from(error).message);
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return AppModalPage(
      title: widget.otherPartyName,
      body: Column(
        children: [
          Expanded(
            child: Builder(
              builder: (context) {
                if (_loading) {
                  return const LoadingView();
                }
                if (_loadError != null) {
                  return ErrorView(message: _loadError!, onRetry: _load);
                }
                final messages = _messages;
                if (messages.isEmpty) {
                  return const EmptyStateView(
                    message: 'Još nema poruka. Napišite prvu poruku ispod.',
                    icon: Icons.chat_bubble_outline_rounded,
                  );
                }
                return RefreshIndicator(
                  onRefresh: _load,
                  child: ListView.builder(
                    physics: const AlwaysScrollableScrollPhysics(),
                    reverse: true,
                    itemCount: messages.length,
                    itemBuilder: (context, index) {
                      final message = messages[messages.length - 1 - index];
                      return Align(
                        alignment: message.isMine
                            ? Alignment.centerRight
                            : Alignment.centerLeft,
                        child: Container(
                          margin: const EdgeInsets.symmetric(vertical: 6),
                          padding: const EdgeInsets.symmetric(
                              horizontal: 16, vertical: 12),
                          constraints: BoxConstraints(
                              maxWidth:
                                  MediaQuery.of(context).size.width * 0.72),
                          decoration: BoxDecoration(
                            color: message.isMine
                                ? AppTheme.accent
                                : AppTheme.panelLight,
                            borderRadius: BorderRadius.circular(18),
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                message.content,
                                style: TextStyle(
                                  color: message.isMine
                                      ? AppTheme.onAccent
                                      : Colors.white,
                                ),
                              ),
                              const SizedBox(height: 4),
                              Text(
                                Formatters.dateTimeLabel(message.sentAt),
                                style: TextStyle(
                                  fontSize: 10.5,
                                  color: message.isMine
                                      ? AppTheme.onAccent.withValues(alpha: 0.7)
                                      : AppTheme.textMuted,
                                ),
                              ),
                            ],
                          ),
                        ),
                      );
                    },
                  ),
                );
              },
            ),
          ),
          if (!widget.canSend)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 10),
              child: Text(
                'Poruke nisu dostupne za ovu saradnju.',
                style: TextStyle(color: AppTheme.textMuted),
              ),
            )
          else
            Form(
              key: _formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (_error != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 6),
                      child: Text(_error!,
                          style: const TextStyle(
                              color: AppTheme.danger, fontSize: 12.5)),
                    ),
                  Row(
                    children: [
                      Expanded(
                        child: TextFormField(
                          controller: _messageController,
                          maxLength: 2000,
                          minLines: 1,
                          maxLines: 4,
                          decoration: const InputDecoration(
                            hintText: 'Napišite poruku...',
                            counterText: '',
                          ),
                          validator: (v) => Validators.textLength(v,
                              min: 1,
                              max: 2000,
                              label: 'Poruka',
                              gender: LabelGender.feminine),
                        ),
                      ),
                      const SizedBox(width: 10),
                      IconButton.filled(
                        onPressed: _sending ? null : _send,
                        style: IconButton.styleFrom(
                            backgroundColor: AppTheme.accent),
                        icon: const Icon(Icons.send_rounded,
                            color: AppTheme.onAccent),
                      ),
                    ],
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}
