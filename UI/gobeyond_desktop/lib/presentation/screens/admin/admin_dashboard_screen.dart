import 'package:flutter/material.dart';

import '../../../core/services/admin_service.dart';
import '../../../core/services/reference_data_service.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/api_error.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/utils/pdf_report.dart';
import '../../widgets/dialogs.dart';
import '../../widgets/panel.dart';
import 'widgets/client_report_dialog.dart';
import 'widgets/mentor_report_dialog.dart';
import 'widgets/report_tables.dart';

/// KONTROLNA PLOČA (Admin) — obavezan izvještajni dio (pravila.md #1):
/// PREGLED (overview + 6-mjesečna zarada + top 5 mentora), MENTORI IZVJEŠTAJ
/// i KLIJENTI IZVJEŠTAJ tabele, sve sa PDF preuzimanjem i printanjem.
class AdminDashboardScreen extends StatefulWidget {
  const AdminDashboardScreen({super.key});

  @override
  State<AdminDashboardScreen> createState() => _AdminDashboardScreenState();
}

class _AdminDashboardScreenState extends State<AdminDashboardScreen> with SingleTickerProviderStateMixin {
  late final TabController _tabController;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 3, vsync: this);
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ContentPanel(
      title: 'KONTROLNA PLOČA',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TabBar(
            controller: _tabController,
            labelColor: AppColors.accent,
            unselectedLabelColor: Colors.white70,
            indicatorColor: AppColors.accent,
            tabs: const [Tab(text: 'PREGLED'), Tab(text: 'MENTORI IZVJEŠTAJ'), Tab(text: 'KLIJENTI IZVJEŠTAJ')],
          ),
          const SizedBox(height: 16),
          Expanded(
            child: TabBarView(
              controller: _tabController,
              children: const [_OverviewTab(), _MentorReportTab(), _ClientReportTab()],
            ),
          ),
        ],
      ),
    );
  }
}

class _OverviewTab extends StatefulWidget {
  const _OverviewTab();

  @override
  State<_OverviewTab> createState() => _OverviewTabState();
}

class _OverviewTabState extends State<_OverviewTab> with AutomaticKeepAliveClientMixin {
  final _service = AdminService();
  bool _loading = true;
  String? _error;
  Map<String, dynamic>? _overview;

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final overview = await _service.getOverviewReport();
      if (!mounted) return;
      setState(() {
        _overview = overview;
        _loading = false;
      });
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
    super.build(context);
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) return EmptyState(message: _error!);
    final overview = _overview!;
    final currency = overview['currency'] as String? ?? 'usd';
    final earnings = (overview['earningsLast6Months'] as List<dynamic>? ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList();
    final topMentors = (overview['topMentors'] as List<dynamic>? ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList();
    double maxEarning = 1;
    for (final e in earnings) {
      final amount = (e['amount'] as num?)?.toDouble() ?? 0;
      if (amount > maxEarning) maxEarning = amount;
    }

    return SingleChildScrollView(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(spacing: 16, runSpacing: 16, children: [
            StatTile(label: 'Broj klijenata', value: '${overview['clientCount'] ?? 0}', icon: Icons.people_outline),
            StatTile(label: 'Broj mentora', value: '${overview['mentorCount'] ?? 0}', icon: Icons.sports_gymnastics),
            StatTile(label: 'Zahtjevi na čekanju', value: '${overview['pendingMentorRequests'] ?? 0}', icon: Icons.pending_actions),
            StatTile(label: 'Aktivne pretplate', value: '${overview['activeSubscriptions'] ?? 0}', icon: Icons.subscriptions_outlined),
            StatTile(label: 'Mjesečna zarada', value: Formatters.money(overview['monthlyEarnings'] as num?, currency: currency), icon: Icons.payments_outlined),
          ]),
          const SizedBox(height: 28),
          const Text('Zarada — posljednjih 6 mjeseci', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 16)),
          const SizedBox(height: 12),
          if (earnings.isEmpty)
            const Text('Nema podataka o zaradi.', style: TextStyle(color: AppColors.textMuted))
          else
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(16)),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.end,
                mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                children: earnings.map((e) {
                  final amount = (e['amount'] as num?)?.toDouble() ?? 0;
                  final heightRatio = maxEarning == 0 ? 0.0 : amount / maxEarning;
                  return Column(
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: [
                      Text(Formatters.money(amount, currency: currency), style: const TextStyle(color: Colors.white, fontSize: 10)),
                      const SizedBox(height: 6),
                      Container(
                        width: 34,
                        height: 24 + heightRatio * 110,
                        decoration: BoxDecoration(color: AppColors.accent, borderRadius: BorderRadius.circular(6)),
                      ),
                      const SizedBox(height: 6),
                      Text('${Formatters.monthName(e['month'] as int).substring(0, 3)} ${e['year']}',
                          style: const TextStyle(color: AppColors.textMuted, fontSize: 10)),
                    ],
                  );
                }).toList(),
              ),
            ),
          const SizedBox(height: 28),
          const Text('Top 5 mentora', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 16)),
          const SizedBox(height: 12),
          if (topMentors.isEmpty)
            const Text('Nema podataka.', style: TextStyle(color: AppColors.textMuted))
          else
            ...topMentors.asMap().entries.map((entry) {
              final m = entry.value;
              return Container(
                margin: const EdgeInsets.only(bottom: 8),
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(12)),
                child: Row(children: [
                  Text('${entry.key + 1}.', style: const TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold)),
                  const SizedBox(width: 12),
                  Expanded(flex: 2, child: Text(m['fullName'] as String? ?? '', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600))),
                  Expanded(child: Text(m['trainingTypeName'] as String? ?? '', style: const TextStyle(color: AppColors.textMuted))),
                  Expanded(child: Row(children: [const Icon(Icons.star, size: 14, color: AppColors.accent), Text(' ${m['averageRating'] ?? 0}', style: const TextStyle(color: Colors.white))])),
                  Expanded(child: Text('${m['activeSubscribers'] ?? 0} pretplatnika', style: const TextStyle(color: Colors.white))),
                ]),
              );
            }),
        ],
      ),
    );
  }
}

class _MentorReportTab extends StatefulWidget {
  const _MentorReportTab();

  @override
  State<_MentorReportTab> createState() => _MentorReportTabState();
}

class _MentorReportTabState extends State<_MentorReportTab> with AutomaticKeepAliveClientMixin {
  final _service = AdminService();
  final _referenceDataService = ReferenceDataService();
  final _searchController = TextEditingController();
  bool _loading = true;
  bool _exporting = false;
  Map<String, dynamic>? _report;
  List<Map<String, dynamic>> _trainingTypes = const [];
  int? _trainingTypeFilter;
  late int _year;
  late int _month;

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _year = now.year;
    _month = now.month;
    _referenceDataService.list(ReferenceResource.trainingTypes).then((v) {
      if (mounted) setState(() => _trainingTypes = v);
    });
    _load();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final report = await _service.getMentorReports(
        search: _searchController.text,
        trainingTypeId: _trainingTypeFilter,
        year: _year,
        month: _month,
      );
      if (!mounted) return;
      setState(() {
        _report = report;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  List<String> _headers() => MentorReportTable.headers;

  List<List<String>> _rows(List<Map<String, dynamic>> items, String currency) {
    return items
        .map((r) => [
              r['fullName'] as String? ?? '',
              r['trainingTypeName'] as String? ?? '',
              '${r['activeSubscribers'] ?? 0}',
              '${r['totalSubscribers'] ?? 0}',
              Formatters.money(r['monthlyEarnings'] as num?, currency: currency),
              Formatters.money(r['totalEarnings'] as num?, currency: currency),
              Formatters.minutesToHoursAndMinutes(r['timeOnPlatformMinutes'] as num?),
              '${r['averageRating'] ?? 0}',
            ])
        .toList();
  }

  Future<void> _export({required bool print}) async {
    final report = _report;
    if (report == null) return;
    setState(() => _exporting = true);
    try {
      final items = (report['items'] as List<dynamic>? ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList();
      final totals = report['totals'] as Map<String, dynamic>? ?? const {};
      final currency = report['currency'] as String? ?? 'usd';
      final bytes = await PdfReport.buildTableReport(
        title: 'Izvještaj o mentorima',
        subtitle: '${Formatters.monthName(_month)} $_year',
        headers: _headers(),
        rows: _rows(items, currency),
        totalsRow: [
          'UKUPNO (${totals['mentorCount'] ?? items.length} mentora)',
          'Aktivnih: ${totals['activeSubscribers'] ?? 0}',
          'Mjesečno: ${Formatters.money(totals['monthlyEarnings'] as num?, currency: currency)}',
          'Ukupno: ${Formatters.money(totals['totalEarnings'] as num?, currency: currency)}',
          'Vrijeme: ${Formatters.minutesToHoursAndMinutes(totals['timeOnPlatformMinutes'] as num?)}',
        ],
      );
      if (print) {
        await PdfReport.print(bytes, 'izvjestaj-mentori');
      } else {
        final savedUri = await PdfReport.save(bytes, 'izvjestaj-mentori-$_year-$_month.pdf');
        // PdfReport.save returns null when the user cancelled the Save
        // dialog — nothing was written, so there is nothing to confirm.
        if (mounted && savedUri != null) showSuccessSnack(context, 'Izvještaj je sačuvan.');
      }
    } catch (error) {
      if (mounted) showErrorSnack(context, ApiError.from(error).message);
    } finally {
      if (mounted) setState(() => _exporting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final report = _report;
    final items = report == null ? <Map<String, dynamic>>[] : (report['items'] as List<dynamic>? ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList();
    final totals = report?['totals'] as Map<String, dynamic>? ?? const {};
    final currency = report?['currency'] as String? ?? 'usd';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(children: [
          Expanded(child: SearchField(controller: _searchController, hintText: 'Pretraga mentora', onSubmitted: (_) => _load())),
          const SizedBox(width: 12),
          SizedBox(
            width: 200,
            child: DropdownButtonFormField<int?>(
              initialValue: _trainingTypeFilter,
              decoration: const InputDecoration(labelText: 'Vrsta treninga'),
              items: [
                const DropdownMenuItem(value: null, child: Text('Sve')),
                ..._trainingTypes.map((t) => DropdownMenuItem(value: t['id'] as int, child: Text(t['name'] as String))),
              ],
              onChanged: (v) {
                setState(() => _trainingTypeFilter = v);
                _load();
              },
            ),
          ),
          const SizedBox(width: 12),
          SizedBox(
            width: 150,
            child: DropdownButtonFormField<int>(
              initialValue: _month,
              decoration: const InputDecoration(labelText: 'Mjesec'),
              items: List.generate(12, (i) => i + 1).map((m) => DropdownMenuItem(value: m, child: Text(Formatters.monthName(m)))).toList(),
              onChanged: (v) {
                setState(() => _month = v!);
                _load();
              },
            ),
          ),
          const SizedBox(width: 12),
          SizedBox(
            width: 110,
            child: DropdownButtonFormField<int>(
              initialValue: _year,
              decoration: const InputDecoration(labelText: 'Godina'),
              items: List.generate(5, (i) => DateTime.now().year - i).map((y) => DropdownMenuItem(value: y, child: Text('$y'))).toList(),
              onChanged: (v) {
                setState(() => _year = v!);
                _load();
              },
            ),
          ),
        ]),
        const SizedBox(height: 12),
        Row(mainAxisAlignment: MainAxisAlignment.end, children: [
          OutlinedButton.icon(onPressed: _exporting ? null : () => _export(print: false), icon: const Icon(Icons.download), label: const Text('PREUZMI PDF')),
          const SizedBox(width: 8),
          ElevatedButton.icon(onPressed: _exporting ? null : () => _export(print: true), icon: const Icon(Icons.print), label: const Text('PRINTAJ')),
        ]),
        const SizedBox(height: 12),
        Expanded(
          child: _loading
              ? const Center(child: CircularProgressIndicator())
              : items.isEmpty
                  ? const EmptyState(message: 'Nema podataka za odabrani period.')
                  : SingleChildScrollView(
                      child: MentorReportTable(
                        items: items,
                        totals: totals,
                        currency: currency,
                        onShowReport: (mentorProfileId, fullName) =>
                            showMentorReportDialog(context, mentorProfileId: mentorProfileId, fullName: fullName),
                      ),
                    ),
        ),
      ],
    );
  }
}

class _ClientReportTab extends StatefulWidget {
  const _ClientReportTab();

  @override
  State<_ClientReportTab> createState() => _ClientReportTabState();
}

class _ClientReportTabState extends State<_ClientReportTab> with AutomaticKeepAliveClientMixin {
  final _service = AdminService();
  final _searchController = TextEditingController();
  bool _loading = true;
  bool _exporting = false;
  Map<String, dynamic>? _report;
  late int _year;
  late int _month;

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _year = now.year;
    _month = now.month;
    _load();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final report = await _service.getClientReports(search: _searchController.text, year: _year, month: _month);
      if (!mounted) return;
      setState(() {
        _report = report;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  List<String> _headers() => ClientReportTable.headers;

  List<List<String>> _rows(List<Map<String, dynamic>> items, String currency) {
    return items
        .map((r) => [
              r['fullName'] as String? ?? '',
              r['activeMentorName'] as String? ?? '-',
              '${r['activeSubscriptions'] ?? 0}',
              '${r['totalSubscriptions'] ?? 0}',
              Formatters.money(r['totalPaid'] as num?, currency: currency),
              '${r['completedTrainings'] ?? 0}',
              '${r['progressEntries'] ?? 0}',
              Formatters.minutesToHoursAndMinutes(r['timeOnPlatformMinutes'] as num?),
            ])
        .toList();
  }

  Future<void> _export({required bool print}) async {
    final report = _report;
    if (report == null) return;
    setState(() => _exporting = true);
    try {
      final items = (report['items'] as List<dynamic>? ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList();
      final totals = report['totals'] as Map<String, dynamic>? ?? const {};
      final currency = report['currency'] as String? ?? 'usd';
      final bytes = await PdfReport.buildTableReport(
        title: 'Izvještaj o klijentima',
        subtitle: '${Formatters.monthName(_month)} $_year',
        headers: _headers(),
        rows: _rows(items, currency),
        totalsRow: [
          'UKUPNO (${totals['clientCount'] ?? items.length} klijenata)',
          'Aktivnih pretplata: ${totals['activeSubscriptions'] ?? 0}',
          'Plaćeno: ${Formatters.money(totals['totalPaid'] as num?, currency: currency)}',
          'Treninzi: ${totals['completedTrainings'] ?? 0}',
          'Vrijeme: ${Formatters.minutesToHoursAndMinutes(totals['timeOnPlatformMinutes'] as num?)}',
        ],
      );
      if (print) {
        await PdfReport.print(bytes, 'izvjestaj-klijenti');
      } else {
        final savedUri = await PdfReport.save(bytes, 'izvjestaj-klijenti-$_year-$_month.pdf');
        if (mounted && savedUri != null) showSuccessSnack(context, 'Izvještaj je sačuvan.');
      }
    } catch (error) {
      if (mounted) showErrorSnack(context, ApiError.from(error).message);
    } finally {
      if (mounted) setState(() => _exporting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final report = _report;
    final items = report == null ? <Map<String, dynamic>>[] : (report['items'] as List<dynamic>? ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList();
    final totals = report?['totals'] as Map<String, dynamic>? ?? const {};
    final currency = report?['currency'] as String? ?? 'usd';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(children: [
          Expanded(child: SearchField(controller: _searchController, hintText: 'Pretraga klijenata', onSubmitted: (_) => _load())),
          const SizedBox(width: 12),
          SizedBox(
            width: 150,
            child: DropdownButtonFormField<int>(
              initialValue: _month,
              decoration: const InputDecoration(labelText: 'Mjesec'),
              items: List.generate(12, (i) => i + 1).map((m) => DropdownMenuItem(value: m, child: Text(Formatters.monthName(m)))).toList(),
              onChanged: (v) {
                setState(() => _month = v!);
                _load();
              },
            ),
          ),
          const SizedBox(width: 12),
          SizedBox(
            width: 110,
            child: DropdownButtonFormField<int>(
              initialValue: _year,
              decoration: const InputDecoration(labelText: 'Godina'),
              items: List.generate(5, (i) => DateTime.now().year - i).map((y) => DropdownMenuItem(value: y, child: Text('$y'))).toList(),
              onChanged: (v) {
                setState(() => _year = v!);
                _load();
              },
            ),
          ),
        ]),
        const SizedBox(height: 12),
        Row(mainAxisAlignment: MainAxisAlignment.end, children: [
          OutlinedButton.icon(onPressed: _exporting ? null : () => _export(print: false), icon: const Icon(Icons.download), label: const Text('PREUZMI PDF')),
          const SizedBox(width: 8),
          ElevatedButton.icon(onPressed: _exporting ? null : () => _export(print: true), icon: const Icon(Icons.print), label: const Text('PRINTAJ')),
        ]),
        const SizedBox(height: 12),
        Expanded(
          child: _loading
              ? const Center(child: CircularProgressIndicator())
              : items.isEmpty
                  ? const EmptyState(message: 'Nema podataka za odabrani period.')
                  : SingleChildScrollView(
                      child: ClientReportTable(
                        items: items,
                        totals: totals,
                        currency: currency,
                        onShowReport: (clientProfileId, fullName) =>
                            showClientReportDialog(context, clientProfileId: clientProfileId, fullName: fullName),
                      ),
                    ),
        ),
      ],
    );
  }
}
