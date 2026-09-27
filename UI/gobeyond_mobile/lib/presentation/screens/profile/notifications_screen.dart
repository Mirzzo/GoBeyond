import 'package:flutter/material.dart';

import '../../../core/network/dio_client.dart';

class NotificationsScreen extends StatefulWidget {
  const NotificationsScreen({super.key});

  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen> {
  final _client = DioClient();
  List<Map<String, dynamic>> _items = [];
  String _search = '';
  String? _error;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final response = await _client.dio.get<List<dynamic>>(
          '/api/notifications',
          queryParameters:
              _search.trim().isEmpty ? null : {'search': _search.trim()});
      if (mounted)
        setState(() => _items =
            (response.data ?? []).whereType<Map<String, dynamic>>().toList());
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _markRead(Map<String, dynamic> item) async {
    try {
      await _client.dio.put<void>('/api/notifications/${item['id']}/read');
      await _load();
    } catch (error) {
      if (mounted)
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(
            content: Text('Obavijest nije označena pročitanom: $error')));
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Obavijesti')),
        body: Column(children: [
          Padding(
            padding: const EdgeInsets.all(16),
            child: TextField(
              decoration: InputDecoration(
                labelText: 'Pretraži obavijesti',
                suffixIcon: IconButton(
                    icon: const Icon(Icons.search), onPressed: _load),
              ),
              onChanged: (value) => _search = value,
              onSubmitted: (_) => _load(),
            ),
          ),
          Expanded(
              child: _loading
                  ? const Center(child: CircularProgressIndicator())
                  : _error != null
                      ? Center(child: Text(_error!))
                      : _items.isEmpty
                          ? const Center(child: Text('Nema obavijesti.'))
                          : RefreshIndicator(
                              onRefresh: _load,
                              child: ListView.builder(
                                itemCount: _items.length,
                                itemBuilder: (context, index) {
                                  final item = _items[index];
                                  return ListTile(
                                    leading: Icon(item['isRead'] == true
                                        ? Icons.notifications_none
                                        : Icons.notifications_active),
                                    title:
                                        Text(item['title']?.toString() ?? ''),
                                    subtitle:
                                        Text(item['body']?.toString() ?? ''),
                                    trailing: item['isRead'] == true
                                        ? null
                                        : IconButton(
                                            icon: const Icon(Icons.done),
                                            tooltip: 'Označi pročitanom',
                                            onPressed: () => _markRead(item),
                                          ),
                                  );
                                },
                              ),
                            )),
        ]),
      );
}
