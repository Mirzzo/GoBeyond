import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/services/panel_api_service.dart';
import '../../../core/session/session_controller.dart';
import '../../widgets/panel_card.dart';

class AdminClientsScreen extends StatefulWidget {
  const AdminClientsScreen({super.key});

  @override
  State<AdminClientsScreen> createState() => _AdminClientsScreenState();
}

class _AdminClientsScreenState extends State<AdminClientsScreen> {
  final PanelApiService _service = PanelApiService(ApiClient());
  final TextEditingController _searchController = TextEditingController();
  List<Map<String, dynamic>> _clients = const [];
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
      final clients = await _service.getClients(token, search: _searchController.text);
      if (!mounted) {
        return;
      }

      setState(() {
        _clients = clients;
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

  Future<void> _blockClient(int userId) async {
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
        const SnackBar(content: Text('Client account blocked.')),
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

  Future<void> _deleteClient(int userId) async {
    final confirmed = await showDialog<bool>(
          context: context,
          builder: (context) {
            return AlertDialog(
              backgroundColor: const Color(0xFF1F1F1F),
              title: const Text('Delete client account?'),
              content: const Text(
                'This deactivates the client account and removes it from active lists.',
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
        const SnackBar(content: Text('Client account deleted.')),
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

  @override
  Widget build(BuildContext context) {
    return PanelCard(
      title: 'Clients',
      description:
          'Client management is live with search, subscription counts, and block/delete actions.',
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
              hintText: 'Search clients',
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
          else if (_clients.isEmpty)
            const Text('No clients match the current search.')
          else
            ..._clients.map(
              (client) => Container(
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
                            client['fullName']?.toString() ?? '-',
                            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            '${client['fitnessLevel'] ?? 'Client'} • ${client['activeSubscriptions']} active subscriptions',
                            style: const TextStyle(color: Color(0xFFBDBDBD)),
                          ),
                          const SizedBox(height: 4),
                          Text(client['email']?.toString() ?? '-'),
                        ],
                      ),
                    ),
                    TextButton(
                      onPressed: _isMutating ? null : () => _blockClient(client['userId'] as int),
                      child: const Text('Block'),
                    ),
                    ElevatedButton(
                      onPressed: _isMutating ? null : () => _deleteClient(client['userId'] as int),
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
