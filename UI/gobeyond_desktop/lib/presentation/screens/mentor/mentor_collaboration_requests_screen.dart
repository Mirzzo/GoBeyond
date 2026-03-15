import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/services/panel_api_service.dart';
import '../../../core/session/session_controller.dart';
import '../../widgets/panel_card.dart';
import 'mentor_create_plan_screen.dart';

class MentorCollaborationRequestsScreen extends StatefulWidget {
  const MentorCollaborationRequestsScreen({super.key});

  @override
  State<MentorCollaborationRequestsScreen> createState() => _MentorCollaborationRequestsScreenState();
}

class _MentorCollaborationRequestsScreenState extends State<MentorCollaborationRequestsScreen> {
  final PanelApiService _service = PanelApiService(ApiClient());
  final TextEditingController _searchController = TextEditingController();
  List<Map<String, dynamic>> _requests = const [];
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
      final items = await _service.getCollaborationRequests(
        token,
        search: _searchController.text,
      );
      if (!mounted) {
        return;
      }

      setState(() {
        _requests = items;
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

  void _showQuestionnaire(Map<String, dynamic> request) {
    final questionnaire = request['questionnaire'] as Map<String, dynamic>? ?? const {};

    showDialog<void>(
      context: context,
      builder: (context) {
        return AlertDialog(
          backgroundColor: const Color(0xFF1F1F1F),
          title: Text(request['clientName']?.toString() ?? 'Questionnaire'),
          content: SizedBox(
            width: 480,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Primary goal: ${questionnaire['primaryGoal']}'),
                Text('Time commitment: ${questionnaire['timeCommitment']}'),
                Text('Availability: ${questionnaire['weeklyAvailability']}'),
                Text('Activity level: ${questionnaire['physicalActivityLevel']}'),
                Text('Health issues: ${questionnaire['healthIssues']}'),
                Text('Medications: ${questionnaire['medications']}'),
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
  }

  Future<void> _openCreatePlan(Map<String, dynamic> request) async {
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => MentorCreatePlanScreen(
          initialSubscriptionId: request['subscriptionId'] as int?,
        ),
      ),
    );

    if (mounted) {
      await _load();
    }
  }

  @override
  Widget build(BuildContext context) {
    return PanelCard(
      title: 'Collaboration Requests',
      description:
          'Pending onboarding requests are now searchable, questionnaire-backed and one click away from plan creation.',
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
              hintText: 'Search requests by client or goal',
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
          else if (_requests.isEmpty)
            const Text('No collaboration requests match the current search.')
          else
            ..._requests.map(
              (request) => Container(
                margin: const EdgeInsets.only(bottom: 12),
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: const Color(0xFF1F1F1F),
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(color: const Color(0x25FFD700)),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      request['clientName']?.toString() ?? '-',
                      style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      '${request['fitnessLevel']} • ${request['amountPaid']} BAM',
                      style: const TextStyle(color: Color(0xFFBDBDBD)),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      (request['questionnaire'] as Map<String, dynamic>?)?['primaryGoal']?.toString() ?? '',
                    ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(
                          child: OutlinedButton(
                            onPressed: () => _showQuestionnaire(request),
                            child: const Text('View questionnaire'),
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: ElevatedButton(
                            onPressed: () => _openCreatePlan(request),
                            child: const Text('Create plan'),
                          ),
                        ),
                      ],
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
