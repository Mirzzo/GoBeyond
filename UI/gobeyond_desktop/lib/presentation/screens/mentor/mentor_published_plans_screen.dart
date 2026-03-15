import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/services/panel_api_service.dart';
import '../../../core/session/session_controller.dart';
import '../../widgets/panel_card.dart';

class MentorPublishedPlansScreen extends StatefulWidget {
  const MentorPublishedPlansScreen({super.key});

  @override
  State<MentorPublishedPlansScreen> createState() => _MentorPublishedPlansScreenState();
}

class _MentorPublishedPlansScreenState extends State<MentorPublishedPlansScreen> {
  final PanelApiService _service = PanelApiService(ApiClient());
  final TextEditingController _searchController = TextEditingController();
  List<Map<String, dynamic>> _plans = const [];
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
      final plans = await _service.getPlans(
        token,
        search: _searchController.text,
        status: 'Published',
      );
      if (!mounted) {
        return;
      }

      setState(() {
        _plans = plans;
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

  Future<void> _showPlan(int planId) async {
    final token = context.read<SessionController>().accessToken;
    if (token == null) {
      return;
    }

    try {
      final detail = await _service.getPlanDetail(token, planId);
      if (!mounted) {
        return;
      }

      final days = (detail['days'] as List<dynamic>? ?? const [])
          .whereType<Map<String, dynamic>>()
          .toList();

      showDialog<void>(
        context: context,
        builder: (context) {
          return AlertDialog(
            backgroundColor: const Color(0xFF1F1F1F),
            title: Text('Week ${detail['weekNumber']} • ${detail['clientName']}'),
            content: SizedBox(
              width: 560,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(detail['motivationalQuote']?.toString() ?? ''),
                  const SizedBox(height: 12),
                  ...days.map(
                    (day) => Padding(
                      padding: const EdgeInsets.only(bottom: 10),
                      child: Text(
                        '${day['dayOfWeek']}: ${day['trainingDescription']}',
                      ),
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
        SnackBar(content: Text('Unable to open plan: $error')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return PanelCard(
      title: 'Published Plans',
      description:
          'Published mentor plans are searchable by client name and open into a live plan preview.',
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
              hintText: 'Search plans by client or quote',
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
          else if (_plans.isEmpty)
            const Text('No published plans match the current search.')
          else
            ..._plans.map(
              (plan) => Container(
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
                            '${plan['clientName']} • Week ${plan['weekNumber']}',
                            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            '${plan['focusTitle']} • ${plan['dayCount']} day(s)',
                            style: const TextStyle(color: Color(0xFFBDBDBD)),
                          ),
                          const SizedBox(height: 4),
                          Text(plan['motivationalQuote']?.toString() ?? ''),
                        ],
                      ),
                    ),
                    ElevatedButton(
                      onPressed: () => _showPlan(plan['id'] as int),
                      child: const Text('Open'),
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
