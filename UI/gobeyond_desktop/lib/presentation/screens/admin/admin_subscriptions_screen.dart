import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/services/panel_api_service.dart';
import '../../../core/session/session_controller.dart';
import '../../widgets/panel_card.dart';

class AdminSubscriptionsScreen extends StatefulWidget {
  const AdminSubscriptionsScreen({super.key});

  @override
  State<AdminSubscriptionsScreen> createState() => _AdminSubscriptionsScreenState();
}

class _AdminSubscriptionsScreenState extends State<AdminSubscriptionsScreen> {
  final PanelApiService _service = PanelApiService(ApiClient());
  final TextEditingController _searchController = TextEditingController();
  List<Map<String, dynamic>> _subscriptions = const [];
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
      final subscriptions = await _service.getSubscriptions(
        token,
        search: _searchController.text,
      );
      if (!mounted) {
        return;
      }

      setState(() {
        _subscriptions = subscriptions;
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

  void _showDetails(Map<String, dynamic> subscription) {
    showDialog<void>(
      context: context,
      builder: (context) {
        return AlertDialog(
          backgroundColor: const Color(0xFF1F1F1F),
          title: Text('Subscription #${subscription['id']}'),
          content: SizedBox(
            width: 460,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Client: ${subscription['clientName']}'),
                Text('Mentor: ${subscription['mentorName']}'),
                Text('Status: ${subscription['status']}'),
                Text('Payment: ${subscription['paymentStatus']}'),
                Text('Primary goal: ${subscription['primaryGoal']}'),
                Text('Published plan: ${subscription['hasPublishedPlan'] == true ? 'Yes' : 'No'}'),
                Text('Period: ${subscription['startDate']} -> ${subscription['endDate']}'),
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

  @override
  Widget build(BuildContext context) {
    return PanelCard(
      title: 'Manage Subscriptions',
      description:
          'The subscription list is API-backed with search and detail review for payment and plan status.',
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
              hintText: 'Search subscriptions by client, mentor or status',
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
          else if (_subscriptions.isEmpty)
            const Text('No subscriptions match the current search.')
          else
            ..._subscriptions.map(
              (subscription) => Container(
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
                            '${subscription['clientName']} -> ${subscription['mentorName']}',
                            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            '${subscription['status']} • ${subscription['paymentStatus']}',
                            style: const TextStyle(color: Color(0xFFBDBDBD)),
                          ),
                          const SizedBox(height: 4),
                          Text(subscription['primaryGoal']?.toString() ?? '-'),
                        ],
                      ),
                    ),
                    TextButton(
                      onPressed: () => _showDetails(subscription),
                      child: const Text('Details'),
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
