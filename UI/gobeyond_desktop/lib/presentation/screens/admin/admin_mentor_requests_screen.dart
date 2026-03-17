import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/services/panel_api_service.dart';
import '../../../core/session/session_controller.dart';
import '../../widgets/panel_card.dart';

class AdminMentorRequestsScreen extends StatefulWidget {
  const AdminMentorRequestsScreen({super.key});

  @override
  State<AdminMentorRequestsScreen> createState() => _AdminMentorRequestsScreenState();
}

class _AdminMentorRequestsScreenState extends State<AdminMentorRequestsScreen> {
  final PanelApiService _service = PanelApiService(ApiClient());
  final TextEditingController _searchController = TextEditingController();
  List<Map<String, dynamic>> _requests = const [];
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
    final session = context.read<SessionController>();

    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final items = await session.runAuthenticated(
        (token) => _service.getMentorRequests(
          token,
          search: _searchController.text,
        ),
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
        setState(() {
          _isLoading = false;
        });
      }
    }
  }

  Future<void> _handleAction(
    Future<Map<String, dynamic>> Function(String token, int id) action,
    int id,
    String message,
  ) async {
    final session = context.read<SessionController>();

    setState(() => _isMutating = true);
    try {
      await session.runAuthenticated((token) => action(token, id));
      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
      await _load();
    } catch (error) {
      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Action failed: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _isMutating = false);
      }
    }
  }

  void _showCertificates(List<Map<String, dynamic>> certificates) {
    showDialog<void>(
      context: context,
      builder: (context) {
        return AlertDialog(
          backgroundColor: const Color(0xFF1F1F1F),
          title: const Text('Certificates'),
          content: SizedBox(
            width: 420,
            child: certificates.isEmpty
                ? const Text('No certificates uploaded.')
                : Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: certificates
                        .map(
                          (certificate) => Padding(
                            padding: const EdgeInsets.only(bottom: 10),
                            child: Text(
                              '${certificate['fileName']} \n${certificate['fileUrl']}',
                            ),
                          ),
                        )
                        .toList(),
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
      title: 'Mentor Requests',
      description:
          'Pending mentor approvals now load from the API with certificate review and approve/reject actions.',
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
              hintText: 'Search mentor requests',
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
            const Text('No mentor requests match the current search.')
          else
            ..._requests.map(
              (item) {
                final certificates = (item['certificates'] as List<dynamic>? ?? const [])
                    .whereType<Map<String, dynamic>>()
                    .toList();

                return Container(
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
                      Row(
                        children: [
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  item['fullName']?.toString() ?? '-',
                                  style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
                                ),
                                const SizedBox(height: 4),
                                Text(
                                  '${item['email']} • ${item['category']} • ${item['price']} BAM',
                                  style: const TextStyle(color: Color(0xFFBDBDBD)),
                                ),
                              ],
                            ),
                          ),
                          TextButton(
                            onPressed: () => _showCertificates(certificates),
                            child: Text('Certificates (${item['certificateCount'] ?? 0})'),
                          ),
                        ],
                      ),
                      const SizedBox(height: 10),
                      Text(item['bio']?.toString() ?? ''),
                      const SizedBox(height: 12),
                      Row(
                        children: [
                          Expanded(
                            child: OutlinedButton(
                              onPressed: _isMutating
                                  ? null
                                  : () => _handleAction(
                                        _service.rejectMentorRequest,
                                        item['id'] as int,
                                        'Mentor request rejected.',
                                      ),
                              child: const Text('Reject'),
                            ),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: ElevatedButton(
                              onPressed: _isMutating
                                  ? null
                                  : () => _handleAction(
                                        _service.approveMentorRequest,
                                        item['id'] as int,
                                        'Mentor request approved.',
                                      ),
                              child: const Text('Approve'),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                );
              },
            ),
        ],
      ),
    );
  }
}
