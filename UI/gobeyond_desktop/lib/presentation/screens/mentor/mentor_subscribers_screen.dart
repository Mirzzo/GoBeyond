import 'package:flutter/material.dart';

import '../../../core/services/mentor_service.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/api_error.dart';
import '../../../core/utils/formatters.dart';
import '../../widgets/avatar.dart';
import '../../widgets/dialogs.dart';
import '../../widgets/panel.dart';
import 'mentor_subscriber_detail_screen.dart';

const _statusOptions = ['Active', 'Expired', 'Cancelled'];

/// PRETPLATNICI.
class MentorSubscribersScreen extends StatefulWidget {
  const MentorSubscribersScreen({super.key});

  @override
  State<MentorSubscribersScreen> createState() => _MentorSubscribersScreenState();
}

class _MentorSubscribersScreenState extends State<MentorSubscribersScreen> {
  final _service = MentorService();
  final _searchController = TextEditingController();
  bool _loading = true;
  List<Map<String, dynamic>> _subscribers = const [];
  String? _statusFilter;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final subscribers = await _service.getSubscribers(search: _searchController.text, status: _statusFilter);
      if (!mounted) return;
      setState(() {
        _subscribers = subscribers;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  @override
  Widget build(BuildContext context) {
    return ContentPanel(
      title: 'PRETPLATNICI',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(children: [
            Expanded(child: SearchField(controller: _searchController, hintText: 'Pretraga pretplatnika', onSubmitted: (_) => _load())),
            const SizedBox(width: 12),
            SizedBox(
              width: 200,
              child: DropdownButtonFormField<String?>(
                initialValue: _statusFilter,
                decoration: const InputDecoration(labelText: 'Status'),
                items: [
                  const DropdownMenuItem(value: null, child: Text('Svi statusi')),
                  ..._statusOptions.map((s) => DropdownMenuItem(value: s, child: Text(SubscriptionStatusPresentation.label(s)))),
                ],
                onChanged: (value) {
                  setState(() => _statusFilter = value);
                  _load();
                },
              ),
            ),
          ]),
          const SizedBox(height: 16),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _subscribers.isEmpty
                    ? const EmptyState(message: 'Nema pretplatnika koji odgovaraju pretrazi.')
                    : ListView.separated(
                        itemCount: _subscribers.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 10),
                        itemBuilder: (context, index) {
                          final subscriber = _subscribers[index];
                          final status = subscriber['status'] as String? ?? '';
                          return Container(
                            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                            decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(14)),
                            child: Row(
                              children: [
                                GbAvatar(imageUrl: subscriber['clientPhotoUrl'] as String?, size: 56),
                                const SizedBox(width: 16),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text(subscriber['clientFullName'] as String? ?? '', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                                      const SizedBox(height: 4),
                                      Text(
                                        '${Formatters.date(subscriber['startDate'] as String?)} - ${Formatters.date(subscriber['endDate'] as String?)}',
                                        style: const TextStyle(color: AppColors.textMuted, fontSize: 12),
                                      ),
                                    ],
                                  ),
                                ),
                                StatusChip(label: SubscriptionStatusPresentation.label(status), color: SubscriptionStatusPresentation.color(status)),
                                const SizedBox(width: 14),
                                PillButton(
                                  label: 'PREGLED..',
                                  onPressed: () => Navigator.of(context).push(MaterialPageRoute(
                                    builder: (_) => MentorSubscriberDetailScreen(
                                      subscriptionId: subscriber['subscriptionId'] as int,
                                      clientFullName: subscriber['clientFullName'] as String? ?? '',
                                    ),
                                  )),
                                ),
                              ],
                            ),
                          );
                        },
                      ),
          ),
        ],
      ),
    );
  }
}
