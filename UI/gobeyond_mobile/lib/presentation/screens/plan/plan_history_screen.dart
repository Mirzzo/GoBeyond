import 'package:flutter/material.dart';

import '../../../core/network/dio_client.dart';

class PlanHistoryScreen extends StatefulWidget {
  const PlanHistoryScreen({super.key});

  @override
  State<PlanHistoryScreen> createState() => _PlanHistoryScreenState();
}

class _PlanHistoryScreenState extends State<PlanHistoryScreen> {
  final _client = DioClient();
  List<Map<String, dynamic>> _plans = [];
  bool _loading = true;
  String _search = '';
  String? _error;

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
          '/api/training-plans/history',
          queryParameters: _search.isEmpty ? null : {'search': _search});
      if (mounted)
        setState(() => _plans =
            (response.data ?? []).whereType<Map<String, dynamic>>().toList());
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _open(int id) async {
    try {
      final response = await _client.dio
          .get<Map<String, dynamic>>('/api/training-plans/history/$id');
      final plan = response.data ?? const <String, dynamic>{};
      if (!mounted) return;
      await showDialog<void>(
          context: context,
          builder: (context) => AlertDialog(
                title: Text(plan['focusTitle']?.toString() ?? 'Trening plan'),
                content: SingleChildScrollView(
                    child: Column(
                        mainAxisSize: MainAxisSize.min,
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                      Text(plan['focusSummary']?.toString() ?? ''),
                      ...((plan['days'] as List<dynamic>? ?? [])
                              .whereType<Map<String, dynamic>>())
                          .map((day) => Padding(
                                padding: const EdgeInsets.only(top: 14),
                                child: Text(
                                    '${day['dayOfWeek'] ?? ''}\n${day['trainingDescription'] ?? ''}\nIshrana: ${day['nutritionDescription'] ?? ''}'
                                    '${day['repetitions'] == null ? '' : '\nUrađena ponavljanja: ${day['repetitions']}'}'),
                              )),
                    ])),
                actions: [
                  TextButton(
                      onPressed: () => Navigator.pop(context),
                      child: const Text('Zatvori'))
                ],
              ));
    } catch (error) {
      if (mounted)
        ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('Plan nije dostupan: $error')));
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Historija planova')),
        body: Column(children: [
          Padding(
              padding: const EdgeInsets.all(16),
              child: TextField(
                decoration: InputDecoration(
                    labelText: 'Pretraži planove',
                    suffixIcon: IconButton(
                        icon: const Icon(Icons.search), onPressed: _load)),
                onChanged: (value) => _search = value,
                onSubmitted: (_) => _load(),
              )),
          Expanded(
              child: _loading
                  ? const Center(child: CircularProgressIndicator())
                  : _error != null
                      ? Center(child: Text(_error!))
                      : _plans.isEmpty
                          ? const Center(
                              child: Text('Nema objavljenih planova.'))
                          : ListView.builder(
                              itemCount: _plans.length,
                              itemBuilder: (context, index) {
                                final plan = _plans[index];
                                return ListTile(
                                  title: Text(plan['focusTitle']?.toString() ??
                                      'Trening plan'),
                                  subtitle: Text(
                                      '${plan['mentorName'] ?? ''} • Sedmica ${plan['weekNumber'] ?? ''} • ${plan['status'] ?? ''}'),
                                  trailing: const Icon(Icons.chevron_right),
                                  onTap: () => _open(plan['id'] as int),
                                );
                              })),
        ]),
      );
}
