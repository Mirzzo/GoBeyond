class MessageThread {
  const MessageThread({
    required this.subscriptionId,
    required this.otherPartyName,
    required this.unreadCount,
    required this.canSend,
    this.otherPartyPhotoUrl,
    this.lastMessage,
    this.lastMessageAt,
  });

  final int subscriptionId;
  final String otherPartyName;
  final String? otherPartyPhotoUrl;
  final String? lastMessage;
  final String? lastMessageAt;
  final int unreadCount;
  final bool canSend;

  factory MessageThread.fromJson(Map<String, dynamic> json) => MessageThread(
        subscriptionId: json['subscriptionId'] as int? ?? 0,
        otherPartyName: json['otherPartyName']?.toString() ?? '',
        otherPartyPhotoUrl: json['otherPartyPhotoUrl']?.toString(),
        lastMessage: json['lastMessage']?.toString(),
        lastMessageAt: json['lastMessageAt']?.toString(),
        unreadCount: json['unreadCount'] as int? ?? 0,
        canSend: json['canSend'] as bool? ?? false,
      );
}

class MessageItem {
  const MessageItem({
    required this.id,
    required this.content,
    required this.sentAt,
    required this.isMine,
    required this.senderName,
  });

  final int id;
  final String content;
  final String sentAt;
  final bool isMine;
  final String senderName;

  factory MessageItem.fromJson(Map<String, dynamic> json) => MessageItem(
        id: json['id'] as int? ?? 0,
        content: json['content']?.toString() ?? '',
        sentAt: json['sentAt']?.toString() ?? '',
        isMine: json['isMine'] as bool? ?? false,
        senderName: json['senderName']?.toString() ?? '',
      );
}
