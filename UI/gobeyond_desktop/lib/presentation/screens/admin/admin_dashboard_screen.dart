import 'dart:io';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/services/panel_api_service.dart';
import '../../../core/session/session_controller.dart';
import '../../widgets/panel_card.dart';

class AdminDashboardScreen extends StatefulWidget {
  const AdminDashboardScreen({super.key});

  @override
  State<AdminDashboardScreen> createState() => _AdminDashboardScreenState();
}

class _AdminDashboardScreenState extends State<AdminDashboardScreen> {
  final PanelApiService _service = PanelApiService(ApiClient());
  Map<String, dynamic>? _overview;
  List<Map<String, dynamic>> _mentors = const [];
  bool _isLoading = true;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _load());
  }

  Future<void> _load() async {
    final session = context.read<SessionController>();

    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final overview = await session.runAuthenticated(
        (token) => _service.getOverviewReport(token),
      );
      final mentors = await session.runAuthenticated(
        (token) => _service.getMentors(token),
      );

      if (!mounted) {
        return;
      }

      setState(() {
        _overview = overview;
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

  Future<void> _exportCsv() async {
    final overview = _overview;
    if (overview == null) {
      return;
    }

    final metrics = (overview['metrics'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .toList();
    final monthlyClients = (overview['monthlyClients'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .toList();
    final monthlyRevenue = (overview['monthlyRevenue'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .toList();

    final buffer = StringBuffer()
      ..writeln('section,label,value,delta')
      ..writeAll(
        metrics.map(
          (metric) =>
              'metric,${metric['label'] ?? ''},${metric['value'] ?? ''},${metric['delta'] ?? ''}',
        ),
        '\n',
      )
      ..writeln()
      ..writeln('section,label,value')
      ..writeAll(
        monthlyClients.map(
          (point) => 'monthly_clients,${point['label'] ?? ''},${point['value'] ?? ''}',
        ),
        '\n',
      )
      ..writeln()
      ..writeAll(
        monthlyRevenue.map(
          (point) => 'monthly_revenue,${point['label'] ?? ''},${point['value'] ?? ''}',
        ),
        '\n',
      );

    final homePath = Platform.environment['USERPROFILE'] ?? Directory.current.path;
    final downloads = Directory('$homePath\\Downloads');
    if (!await downloads.exists()) {
      await downloads.create(recursive: true);
    }

    final file = File('${downloads.path}\\gobeyond-overview-report.csv');
    await file.writeAsString(buffer.toString(), flush: true);

    if (!mounted) {
      return;
    }

    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text('Overview report exported to ${file.path}')),
    );
  }

  void _showPreview() {
    final overview = _overview;
    if (overview == null) {
      return;
    }

    final metrics = (overview['metrics'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .toList();
    final alerts = (overview['alerts'] as List<dynamic>? ?? const []).cast<String>();

    showDialog<void>(
      context: context,
      builder: (context) {
        return AlertDialog(
          backgroundColor: const Color(0xFF1F1F1F),
          title: const Text('Report preview'),
          content: SizedBox(
            width: 420,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                ...metrics.map(
                  (metric) => Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: Text(
                      '${metric['label']}: ${metric['value']} (${metric['delta']})',
                    ),
                  ),
                ),
                if (alerts.isNotEmpty) ...[
                  const SizedBox(height: 12),
                  const Text(
                    'Alerts',
                    style: TextStyle(fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(height: 8),
                  ...alerts.map((alert) => Text('- $alert')),
                ],
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

  Future<void> _showMentorReport(Map<String, dynamic> mentor) async {
    final session = context.read<SessionController>();
    final profileId = mentor['profileId'] as int?;
    if (profileId == null) {
      return;
    }

    try {
      final report = await session.runAuthenticated(
        (token) => _service.getMentorReport(token, profileId),
      );
      if (!mounted) {
        return;
      }

      final recentClients = (report['recentClients'] as List<dynamic>? ?? const [])
          .whereType<Map<String, dynamic>>()
          .toList();

      showDialog<void>(
        context: context,
        builder: (context) {
          return AlertDialog(
            backgroundColor: const Color(0xFF1F1F1F),
            title: Text(report['mentorName']?.toString() ?? 'Mentor report'),
            content: SizedBox(
              width: 520,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Category: ${report['category']}'),
                  Text('Status: ${report['status']}'),
                  Text('Active subscribers: ${report['activeSubscribers']}'),
                  Text('Pending requests: ${report['pendingRequests']}'),
                  Text('Published plans: ${report['publishedPlans']}'),
                  Text('Revenue: ${report['totalRevenue']} BAM'),
                  Text('Rating: ${report['averageRating']} (${report['reviewCount']} reviews)'),
                  const SizedBox(height: 16),
                  const Text(
                    'Recent clients',
                    style: TextStyle(fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(height: 8),
                  if (recentClients.isEmpty)
                    const Text('No recent clients available.')
                  else
                    ...recentClients.map(
                      (client) => Padding(
                        padding: const EdgeInsets.only(bottom: 8),
                        child: Text(
                          '${client['clientName']} • ${client['goal']} • ${client['status']}',
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
        SnackBar(content: Text('Unable to load mentor report: $error')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final metrics = (_overview?['metrics'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .toList();
    final monthlyClients = (_overview?['monthlyClients'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .toList();
    final monthlyRevenue = (_overview?['monthlyRevenue'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .toList();
    final alerts = (_overview?['alerts'] as List<dynamic>? ?? const []).cast<String>();

    return PanelCard(
      title: 'Dashboard',
      description:
          'Admin reporting is live here: platform metrics, alerts, mentor drilldowns and CSV export.',
      actions: [
        TextButton(onPressed: _showPreview, child: const Text('Preview')),
        TextButton(onPressed: _exportCsv, child: const Text('Export CSV')),
        IconButton(onPressed: _load, icon: const Icon(Icons.refresh_rounded)),
      ],
      child: _isLoading
          ? const Center(child: Padding(padding: EdgeInsets.all(40), child: CircularProgressIndicator()))
          : _errorMessage != null
              ? Text(
                  _errorMessage!,
                  style: const TextStyle(color: Colors.redAccent),
                )
              : Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Wrap(
                      spacing: 12,
                      runSpacing: 12,
                      children: metrics
                          .map(
                            (metric) => _MetricCard(
                              label: metric['label']?.toString() ?? '-',
                              value: metric['value']?.toString() ?? '-',
                              delta: metric['delta']?.toString() ?? '',
                            ),
                          )
                          .toList(),
                    ),
                    const SizedBox(height: 20),
                    _TrendSection(
                      title: 'Monthly clients',
                      items: monthlyClients,
                    ),
                    const SizedBox(height: 14),
                    _TrendSection(
                      title: 'Monthly revenue',
                      items: monthlyRevenue,
                    ),
                    const SizedBox(height: 20),
                    if (alerts.isNotEmpty) ...[
                      const Text(
                        'Alerts',
                        style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
                      ),
                      const SizedBox(height: 8),
                      ...alerts.map(
                        (alert) => Padding(
                          padding: const EdgeInsets.only(bottom: 6),
                          child: Text('- $alert'),
                        ),
                      ),
                      const SizedBox(height: 20),
                    ],
                    const Text(
                      'Mentor reports',
                      style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
                    ),
                    const SizedBox(height: 10),
                    ..._mentors.take(4).map(
                      (mentor) => Container(
                        margin: const EdgeInsets.only(bottom: 10),
                        padding: const EdgeInsets.all(14),
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
                                    style: const TextStyle(fontWeight: FontWeight.w700),
                                  ),
                                  const SizedBox(height: 4),
                                  Text(
                                    '${mentor['status']} • ${mentor['activeSubscriptions']} active subscriptions',
                                    style: const TextStyle(color: Color(0xFFBDBDBD)),
                                  ),
                                ],
                              ),
                            ),
                            TextButton(
                              onPressed: () => _showMentorReport(mentor),
                              child: const Text('Open report'),
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

class _MetricCard extends StatelessWidget {
  const _MetricCard({
    required this.label,
    required this.value,
    required this.delta,
  });

  final String label;
  final String value;
  final String delta;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 210,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: const Color(0xFF1F1F1F),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: const Color(0x25FFD700)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(color: Color(0xFFBDBDBD))),
          const SizedBox(height: 10),
          Text(
            value,
            style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 6),
          Text(delta, style: const TextStyle(color: Color(0xFFFFD700))),
        ],
      ),
    );
  }
}

class _TrendSection extends StatelessWidget {
  const _TrendSection({
    required this.title,
    required this.items,
  });

  final String title;
  final List<Map<String, dynamic>> items;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFF1F1F1F),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: const Color(0x25FFD700)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 10),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: items
                .map(
                  (item) => Chip(
                    label: Text('${item['label']}: ${item['value']}'),
                  ),
                )
                .toList(),
          ),
        ],
      ),
    );
  }
}
