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

class _MessageChatScreenState extends State<MessageChatScreen> {
  late final MessageRepository _repository =
      widget.messageRepository ?? ApiMessageRepository();
  final _formKey = GlobalKey<FormState>();
  final _messageController = TextEditingController();
  bool _sending = false;
  String? _error;

  late Future<List<MessageItem>> _future;

  @override
  void initState() {
    super.initState();
    _future = _repository.getThreadMessages(widget.subscriptionId);
  }

  @override
  void dispose() {
    _messageController.dispose();
    super.dispose();
  }

  void _reload() => setState(() {
        _future = _repository.getThreadMessages(widget.subscriptionId);
      });

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
      _reload();
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
            child: FutureBuilder<List<MessageItem>>(
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
                final messages = snapshot.data!;
                if (messages.isEmpty) {
                  return const EmptyStateView(
                    message: 'Još nema poruka. Napišite prvu poruku ispod.',
                    icon: Icons.chat_bubble_outline_rounded,
                  );
                }
                return ListView.builder(
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
                            maxWidth: MediaQuery.of(context).size.width * 0.72),
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
                              min: 1, max: 2000, label: 'Poruka'),
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
