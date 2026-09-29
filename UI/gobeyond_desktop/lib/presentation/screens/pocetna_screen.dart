import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/services/admin_service.dart';
import '../../core/services/mentor_service.dart';
import '../../core/session/session_controller.dart';
import '../../core/theme/app_theme.dart';
import '../../core/utils/api_error.dart';
import '../../core/utils/formatters.dart';
import '../widgets/panel.dart';

/// POČETNA: welcome message + key stats, tailored per role.
class PocetnaScreen extends StatefulWidget {
  const PocetnaScreen({super.key});

  @override
  State<PocetnaScreen> createState() => _PocetnaScreenState();
}

class _PocetnaScreenState extends State<PocetnaScreen> {
  bool _loading = true;
  String? _error;
  Map<String, dynamic>? _adminOverview;
  int _mentorPendingRequests = 0;
  int _mentorActiveSubscribers = 0;
  int _mentorPublishedPlans = 0;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final session = context.read<SessionController>();
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      if (session.isAdmin) {
        _adminOverview = await AdminService().getOverviewReport();
      } else {
        final mentorService = MentorService();
        final results = await Future.wait([
          mentorService.getCollaborationRequests(),
          mentorService.getSubscribers(status: 'Active'),
          mentorService.getPlans(status: 'Published'),
        ]);
        _mentorPendingRequests = results[0].length;
        _mentorActiveSubscribers = results[1].length;
        _mentorPublishedPlans = results[2].length;
      }
      if (!mounted) return;
      setState(() => _loading = false);
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = ApiError.from(error).message;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final session = context.watch<SessionController>();
    final user = session.user;

    return ContentPanel(
      title: 'POČETNA',
      child: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? EmptyState(message: _error!)
              : SingleChildScrollView(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Dobrodošli, ${user?.fullName ?? ''}!',
                        style: const TextStyle(color: Colors.white, fontSize: 22, fontWeight: FontWeight.bold),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        session.isAdmin
                            ? 'Pregled ključnih pokazatelja platforme GoBeyond.'
                            : 'Pregled vaših trenutnih aktivnosti na platformi GoBeyond.',
                        style: const TextStyle(color: AppColors.textMuted),
                      ),
                      const SizedBox(height: 24),
                      Wrap(
                        spacing: 16,
                        runSpacing: 16,
                        children: session.isAdmin ? _adminStats() : _mentorStats(),
                      ),
                    ],
                  ),
                ),
    );
  }

  List<Widget> _adminStats() {
    final overview = _adminOverview ?? const {};
    return [
      StatTile(label: 'Broj klijenata', value: '${overview['clientCount'] ?? 0}', icon: Icons.people_outline),
      StatTile(label: 'Broj mentora', value: '${overview['mentorCount'] ?? 0}', icon: Icons.sports_gymnastics),
      StatTile(
          label: 'Zahtjevi za mentora na čekanju',
          value: '${overview['pendingMentorRequests'] ?? 0}',
          icon: Icons.pending_actions),
      StatTile(label: 'Aktivne pretplate', value: '${overview['activeSubscriptions'] ?? 0}', icon: Icons.subscriptions_outlined),
      StatTile(
        label: 'Mjesečna zarada',
        value: Formatters.money(overview['monthlyEarnings'] as num?, currency: overview['currency'] as String? ?? 'usd'),
        icon: Icons.payments_outlined,
      ),
    ];
  }

  List<Widget> _mentorStats() {
    return [
      StatTile(label: 'Zahtjevi za suradnju', value: '$_mentorPendingRequests', icon: Icons.forum_outlined),
      StatTile(label: 'Aktivni pretplatnici', value: '$_mentorActiveSubscribers', icon: Icons.people_outline),
      StatTile(label: 'Objavljeni planovi', value: '$_mentorPublishedPlans', icon: Icons.fact_check_outlined),
    ];
  }
}
