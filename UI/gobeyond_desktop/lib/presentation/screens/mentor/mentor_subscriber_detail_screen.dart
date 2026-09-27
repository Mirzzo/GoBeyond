import 'package:flutter/material.dart';

import '../../../core/config/app_config.dart';
import '../../../core/services/mentor_service.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/api_error.dart';
import '../../../core/utils/formatters.dart';
import '../../widgets/avatar.dart';
import 'mentor_messages_screen.dart';

/// Dedicated (pushed, with Back) subscriber detail: client description,
/// training session log, monthly progress entries with photos, and a
/// shortcut into the chat with this client.
class MentorSubscriberDetailScreen extends StatefulWidget {
  const MentorSubscriberDetailScreen({super.key, required this.subscriptionId, required this.clientFullName});

  final int subscriptionId;
  final String clientFullName;

  @override
  State<MentorSubscriberDetailScreen> createState() => _MentorSubscriberDetailScreenState();
}

class _MentorSubscriberDetailScreenState extends State<MentorSubscriberDetailScreen> {
  final _service = MentorService();
  bool _loading = true;
  String? _error;
  Map<String, dynamic>? _detail;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final detail = await _service.getSubscriberDetail(widget.subscriptionId);
      if (!mounted) return;
      setState(() {
        _detail = detail;
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
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.clientFullName),
        backgroundColor: AppColors.panel,
        actions: [
          TextButton.icon(
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute(
                builder: (_) => Scaffold(
                  appBar: AppBar(title: const Text('Poruke'), backgroundColor: AppColors.panel),
                  body: Padding(
                    padding: const EdgeInsets.all(20),
                    child: MentorMessagesScreen(initialSubscriptionId: widget.subscriptionId),
                  ),
                ),
              ),
            ),
            icon: const Icon(Icons.chat_bubble_outline),
            label: const Text('Poruke'),
          ),
          const SizedBox(width: 12),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Text(_error!, style: const TextStyle(color: AppColors.danger)))
              : _buildBody(_detail!),
    );
  }

  Widget _buildBody(Map<String, dynamic> detail) {
    final sessions = (detail['sessions'] as List<dynamic>? ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList();
    final progress = (detail['progress'] as List<dynamic>? ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList();

    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.all(20),
            decoration: BoxDecoration(color: AppColors.panel, borderRadius: BorderRadius.circular(18)),
            child: Row(children: [
              GbAvatar(imageUrl: detail['clientPhotoUrl'] as String?, size: 80),
              const SizedBox(width: 20),
              Expanded(
                child: Wrap(spacing: 24, runSpacing: 8, children: [
                  _field('Godine', '${detail['age'] ?? '-'}'),
                  _field('Spol', detail['genderName'] as String? ?? '-'),
                  _field('Tjelesna težina', '${detail['weightKg'] ?? '-'} kg'),
                  _field('Visina', '${detail['heightCm'] ?? '-'} cm'),
                  _field('Nivo spreme', detail['fitnessLevelName'] as String? ?? '-'),
                  _field('Cilj', detail['fitnessGoalName'] as String? ?? '-'),
                  _field('Početak saradnje', Formatters.date(detail['startDate'] as String?)),
                  _field('Kraj saradnje', Formatters.date(detail['endDate'] as String?)),
                ]),
              ),
            ]),
          ),
          const SizedBox(height: 24),
          const Text('Evidencija treninga', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 16)),
          const SizedBox(height: 12),
          if (sessions.isEmpty)
            const Text('Klijent još nije evidentirao nijedan trening.', style: TextStyle(color: AppColors.textMuted))
          else
            Container(
              decoration: BoxDecoration(color: AppColors.panel, borderRadius: BorderRadius.circular(16)),
              child: DataTable(
                columns: const [
                  DataColumn(label: Text('Dan')),
                  DataColumn(label: Text('Datum')),
                  DataColumn(label: Text('Ponavljanja')),
                  DataColumn(label: Text('Napomena')),
                ],
                rows: sessions
                    .map((s) => DataRow(cells: [
                          DataCell(Text(s['dayName'] as String? ?? '')),
                          DataCell(Text(Formatters.dateTime(s['completedAt'] as String?))),
                          DataCell(Text('${s['repetitions'] ?? 0}')),
                          DataCell(Text(s['note'] as String? ?? '-')),
                        ]))
                    .toList(),
              ),
            ),
          const SizedBox(height: 24),
          const Text('Mjesečni napredak', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 16)),
          const SizedBox(height: 12),
          if (progress.isEmpty)
            const Text('Klijent još nije dodao unose napretka.', style: TextStyle(color: AppColors.textMuted))
          else
            Wrap(
              spacing: 14,
              runSpacing: 14,
              children: progress.map((p) {
                final photoUrl = AppConfig.resolveUrl(p['photoUrl'] as String?);
                return Container(
                  width: 220,
                  padding: const EdgeInsets.all(14),
                  decoration: BoxDecoration(color: AppColors.panel, borderRadius: BorderRadius.circular(14)),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('${p['monthName']} ${p['year']}', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                      const SizedBox(height: 8),
                      if (photoUrl != null)
                        ClipRRect(
                          borderRadius: BorderRadius.circular(10),
                          child: Image.network(photoUrl, height: 130, width: double.infinity, fit: BoxFit.cover),
                        ),
                      const SizedBox(height: 8),
                      Text('Težina: ${p['weightKg'] ?? '-'} kg', style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
                      Text('Obimi: ${p['measurements'] ?? '-'}', style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
                      Text('Snaga: ${p['strength'] ?? '-'}', style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
                      Text('Kondicija: ${p['conditioning'] ?? '-'}', style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
                    ],
                  ),
                );
              }).toList(),
            ),
        ],
      ),
    );
  }

  Widget _field(String label, String value) {
    return SizedBox(
      width: 170,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
          Text(value, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600)),
        ],
      ),
    );
  }
}
