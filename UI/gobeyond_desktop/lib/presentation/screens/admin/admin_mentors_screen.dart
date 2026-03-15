import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/services/panel_api_service.dart';
import '../../../core/session/session_controller.dart';
import '../../widgets/panel_card.dart';

class AdminMentorsScreen extends StatefulWidget {
  const AdminMentorsScreen({super.key});

  @override
  State<AdminMentorsScreen> createState() => _AdminMentorsScreenState();
}

class _AdminMentorsScreenState extends State<AdminMentorsScreen> {
  final PanelApiService _service = PanelApiService(ApiClient());
  final TextEditingController _searchController = TextEditingController();
  List<Map<String, dynamic>> _mentors = const [];
  bool _isLoading = true;
  bool _isMutating = false;
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
      final mentors = await _service.getMentors(token, search: _searchController.text);
      if (!mounted) {
        return;
      }

      setState(() {
        _mentors = mentors;
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
        setState(() {
          _isLoading = false;
        });
      }
    }
  }

  Future<void> _blockUser(int userId) async {
    final token = context.read<SessionController>().accessToken;
    if (token == null) {
      return;
    }

    setState(() => _isMutating = true);
    try {
      await _service.blockUser(token, userId);
      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Mentor account blocked.')),
      );
      await _load();
    } catch (error) {
      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Block failed: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _isMutating = false);
      }
    }
  }

  Future<void> _deleteUser(int userId) async {
    final confirmed = await showDialog<bool>(
          context: context,
          builder: (context) {
            return AlertDialog(
              backgroundColor: const Color(0xFF1F1F1F),
              title: const Text('Delete mentor account?'),
              content: const Text(
                'This action deactivates the mentor account and removes it from the active panel lists.',
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.of(context).pop(false),
                  child: const Text('Cancel'),
                ),
                ElevatedButton(
                  onPressed: () => Navigator.of(context).pop(true),
                  child: const Text('Delete'),
                ),
              ],
            );
          },
        ) ??
        false;

    if (!confirmed || !mounted) {
      return;
    }

    final token = context.read<SessionController>().accessToken;
    if (token == null) {
      return;
    }

    setState(() => _isMutating = true);
    try {
      await _service.deleteUser(token, userId);
      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Mentor account deleted.')),
      );
      await _load();
    } catch (error) {
      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Delete failed: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _isMutating = false);
      }
    }
  }

  Future<void> _showReport(Map<String, dynamic> mentor) async {
    final token = context.read<SessionController>().accessToken;
    final profileId = mentor['profileId'] as int?;
    if (token == null || profileId == null) {
      return;
    }

    try {
      final report = await _service.getMentorReport(token, profileId);
      if (!mounted) {
        return;
      }

      showDialog<void>(
        context: context,
        builder: (context) {
          return AlertDialog(
            backgroundColor: const Color(0xFF1F1F1F),
            title: Text(report['mentorName']?.toString() ?? 'Mentor report'),
            content: SizedBox(
              width: 440,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Category: ${report['category']}'),
                  Text('Status: ${report['status']}'),
                  Text('Subscribers: ${report['activeSubscribers']}'),
                  Text('Pending requests: ${report['pendingRequests']}'),
                  Text('Published plans: ${report['publishedPlans']}'),
                  Text('Revenue: ${report['totalRevenue']} BAM'),
                  Text('Average rating: ${report['averageRating']}'),
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
        SnackBar(content: Text('Unable to load report: $error')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return PanelCard(
      title: 'Active Mentors',
      description:
          'Search, review mentor reporting, and deactivate mentors directly from the real admin API.',
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
              hintText: 'Search mentors',
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
          else if (_mentors.isEmpty)
            const Text('No mentors match the current search.')
          else
            ..._mentors.map(
              (mentor) => Container(
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
                            mentor['fullName']?.toString() ?? '-',
                            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            '${mentor['category'] ?? 'General'} • ${mentor['status']} • ${mentor['activeSubscriptions']} active',
                            style: const TextStyle(color: Color(0xFFBDBDBD)),
                          ),
                          const SizedBox(height: 4),
                          Text(mentor['email']?.toString() ?? '-'),
                        ],
                      ),
                    ),
                    TextButton(
                      onPressed: () => _showReport(mentor),
                      child: const Text('Report'),
                    ),
                    TextButton(
                      onPressed: _isMutating ? null : () => _blockUser(mentor['userId'] as int),
                      child: const Text('Block'),
                    ),
                    ElevatedButton(
                      onPressed: _isMutating ? null : () => _deleteUser(mentor['userId'] as int),
                      child: const Text('Delete'),
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
