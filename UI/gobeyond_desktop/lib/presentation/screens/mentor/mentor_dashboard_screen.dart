import 'package:flutter/material.dart';

import '../../../core/services/mentor_service.dart';
import '../../../core/services/message_service.dart';
import '../../../core/services/notification_service.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/api_error.dart';
import '../../../core/utils/formatters.dart';
import '../../widgets/panel.dart';

/// KONTROLNA PLOČA (Mentor) — notification overview + activity counts.
class MentorDashboardScreen extends StatefulWidget {
  const MentorDashboardScreen({super.key});

  @override
  State<MentorDashboardScreen> createState() => _MentorDashboardScreenState();
}

class _MentorDashboardScreenState extends State<MentorDashboardScreen> {
  bool _loading = true;
  String? _error;
  List<Map<String, dynamic>> _notifications = const [];
  int _pendingRequests = 0;
  int _activeSubscribers = 0;
  int _publishedPlans = 0;
  int _draftPlans = 0;
  int _unreadThreads = 0;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final mentorService = MentorService();
      final results = await Future.wait([
        NotificationService().getNotifications(unreadOnly: true),
        mentorService.getCollaborationRequests(),
        mentorService.getSubscribers(status: 'Active'),
        mentorService.getPlans(status: 'Published'),
        mentorService.getPlans(status: 'Draft'),
        MessageService().getThreads(),
      ]);
      final notifications = results[0];
      final threads = results[5];
      if (!mounted) return;
      setState(() {
        _notifications = notifications;
        _pendingRequests = results[1].length;
        _activeSubscribers = results[2].length;
        _publishedPlans = results[3].length;
        _draftPlans = results[4].length;
        _unreadThreads = threads.where((t) => (t['unreadCount'] as int? ?? 0) > 0).length;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = ApiError.from(error).message;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return ContentPanel(
      title: 'KONTROLNA PLOČA',
      child: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? EmptyState(message: _error!)
              : SingleChildScrollView(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Wrap(spacing: 16, runSpacing: 16, children: [
                        StatTile(label: 'Zahtjevi za suradnju', value: '$_pendingRequests', icon: Icons.forum_outlined),
                        StatTile(label: 'Aktivni pretplatnici', value: '$_activeSubscribers', icon: Icons.people_outline),
                        StatTile(label: 'Objavljeni planovi', value: '$_publishedPlans', icon: Icons.fact_check_outlined),
                        StatTile(label: 'Nedovršeni planovi', value: '$_draftPlans', icon: Icons.edit_note),
                        StatTile(label: 'Nepročitane poruke (razgovori)', value: '$_unreadThreads', icon: Icons.mark_chat_unread_outlined),
                      ]),
                      const SizedBox(height: 28),
                      const Text('Nepročitane obavijesti', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 16)),
                      const SizedBox(height: 12),
                      if (_notifications.isEmpty)
                        const Text('Nema novih obavijesti.', style: TextStyle(color: AppColors.textMuted))
                      else
                        ..._notifications.take(10).map(
                              (n) => Container(
                                margin: const EdgeInsets.only(bottom: 8),
                                padding: const EdgeInsets.all(14),
                                decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(12)),
                                child: Row(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    const Icon(Icons.notifications_active, color: AppColors.accent, size: 18),
                                    const SizedBox(width: 10),
                                    Expanded(
                                      child: Column(
                                        crossAxisAlignment: CrossAxisAlignment.start,
                                        children: [
                                          Text(n['title'] as String? ?? '', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                                          Text(n['body'] as String? ?? '', style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
                                        ],
                                      ),
                                    ),
                                    Text(Formatters.dateTime(n['createdAt'] as String?), style: const TextStyle(color: AppColors.textMuted, fontSize: 11)),
                                  ],
                                ),
                              ),
                            ),
                    ],
                  ),
                ),
    );
  }
}
