import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/services/panel_api_service.dart';
import '../../../core/session/session_controller.dart';
import '../../widgets/panel_card.dart';

class MentorSubscribersScreen extends StatefulWidget {
  const MentorSubscribersScreen({super.key});

  @override
  State<MentorSubscribersScreen> createState() => _MentorSubscribersScreenState();
}

class _MentorSubscribersScreenState extends State<MentorSubscribersScreen> {
  final PanelApiService _service = PanelApiService(ApiClient());
  final TextEditingController _searchController = TextEditingController();
  List<Map<String, dynamic>> _subscribers = const [];
  bool _isLoading = true;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _load());
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    final token = context.read<SessionController>().accessToken;
    if (token == null) {
      return;
    }

    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final items = await _service.getSubscribers(
        token,
        search: _searchController.text,
      );
      if (!mounted) {
        return;
      }

      setState(() {
        _subscribers = items;
      });
    } catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _errorMessage = error.toString();
      });
    } finally {
      if (mounted) {
        setState(() => _isLoading = false);
      }
    }
  }

  Future<void> _showClientDetail(Map<String, dynamic> subscriber) async {
    final token = context.read<SessionController>().accessToken;
    final clientUserId = subscriber['clientUserId'] as int?;
    if (token == null || clientUserId == null) {
      return;
    }

    try {
      final detail = await _service.getClientDetail(token, clientUserId);
      if (!mounted) {
        return;
      }

      final progress = (detail['recentProgress'] as List<dynamic>? ?? const [])
          .whereType<Map<String, dynamic>>()
          .toList();

      showDialog<void>(
        context: context,
        builder: (context) {
          return AlertDialog(
            backgroundColor: const Color(0xFF1F1F1F),
            title: Text(detail['fullName']?.toString() ?? 'Client detail'),
            content: SizedBox(
              width: 520,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Email: ${detail['email']}'),
                  Text('Fitness level: ${detail['fitnessLevel']}'),
                  Text('Subscription: ${detail['subscriptionStatus']}'),
                  Text('Weight / Height: ${detail['weight']} kg / ${detail['height']} cm'),
                  const SizedBox(height: 12),
                  const Text(
                    'Recent progress',
                    style: TextStyle(fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(height: 8),
                  if (progress.isEmpty)
                    const Text('No progress entries yet.')
                  else
                    ...progress.map(
                      (entry) => Padding(
                        padding: const EdgeInsets.only(bottom: 8),
                        child: Text('${entry['title']} • ${entry['metric']}'),
                      ),
                    ),
                ],
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(context).pop(),
                child: const Text('Close'),
              ),
            ],
          );
        },
      );
    } catch (error) {
      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Unable to load client detail: $error')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return PanelCard(
      title: 'Subscribers',
      description:
          'Active subscribers now load from the mentor API with direct client detail access.',
      actions: [
        IconButton(onPressed: _load, icon: const Icon(Icons.refresh_rounded)),
      ],
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          TextField(
            controller: _searchController,
            onSubmitted: (_) => _load(),
            decoration: InputDecoration(
              hintText: 'Search subscribers by client or goal',
              suffixIcon: IconButton(
                onPressed: _load,
                icon: const Icon(Icons.search_rounded),
              ),
            ),
          ),
          const SizedBox(height: 16),
          if (_isLoading)
            const Center(
              child: Padding(
                padding: EdgeInsets.all(32),
                child: CircularProgressIndicator(),
              ),
            )
          else if (_errorMessage != null)
            Text(
              _errorMessage!,
              style: const TextStyle(color: Colors.redAccent),
            )
          else if (_subscribers.isEmpty)
            const Text('No subscribers match the current search.')
          else
            ..._subscribers.map(
              (subscriber) => Container(
                margin: const EdgeInsets.only(bottom: 12),
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: const Color(0xFF1F1F1F),
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(color: const Color(0x25FFD700)),
                ),
                child: Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            subscriber['clientName']?.toString() ?? '-',
                            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            '${subscriber['status']} • ${subscriber['primaryGoal']}',
                            style: const TextStyle(color: Color(0xFFBDBDBD)),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            subscriber['lastCheckInLabel']?.toString() ?? 'No check-in yet',
                          ),
                        ],
                      ),
                    ),
                    ElevatedButton(
                      onPressed: () => _showClientDetail(subscriber),
                      child: const Text('Client detail'),
                    ),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }
}
