import 'package:flutter/material.dart';

import '../../../../core/services/admin_service.dart';
import '../../../../core/theme/app_theme.dart';
import '../../../../core/utils/api_error.dart';
import '../../../../core/utils/formatters.dart';
import '../../../../core/utils/pdf_report.dart';
import '../../../widgets/dialogs.dart';
import '../../../widgets/panel.dart';

/// Admin "IZVJEŠTAJ" popup for a single mentor (prijava 3.1.2): broj
/// pretplatnika, mjesečna zarada, ukupna zarada, vrijeme provedeno na
/// stranici, 6-mjesečni pregled, i PDF preuzimanje/print.
Future<void> showMentorReportDialog(BuildContext context, {required int mentorProfileId, required String fullName}) {
  return showDialog<void>(
    context: context,
    builder: (_) => _MentorReportDialog(mentorProfileId: mentorProfileId, fullName: fullName),
  );
}

class _MentorReportDialog extends StatefulWidget {
  const _MentorReportDialog({required this.mentorProfileId, required this.fullName});
  final int mentorProfileId;
  final String fullName;

  @override
  State<_MentorReportDialog> createState() => _MentorReportDialogState();
}

class _MentorReportDialogState extends State<_MentorReportDialog> {
  final _service = AdminService();
  bool _loading = true;
  String? _error;
  Map<String, dynamic>? _report;
  int? _year;
  int? _month;
  bool _exporting = false;

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _year = now.year;
    _month = now.month;
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final report = await _service.getMentorReportDetail(widget.mentorProfileId, year: _year, month: _month);
      if (!mounted) return;
      setState(() {
        _report = report;
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

  List<MapEntry<String, String>> _fields(Map<String, dynamic> r) {
    final currency = r['currency'] as String? ?? 'usd';
    return [
      MapEntry('Mentor', r['fullName'] as String? ?? widget.fullName),
      MapEntry('Vrsta treninga', r['trainingTypeName'] as String? ?? '-'),
      MapEntry('Broj aktivnih pretplatnika', '${r['activeSubscribers'] ?? 0}'),
      MapEntry('Ukupan broj pretplatnika', '${r['totalSubscribers'] ?? 0}'),
      MapEntry('Mjesečna zarada', Formatters.money(r['monthlyEarnings'] as num?, currency: currency)),
      MapEntry('Ukupna zarada', Formatters.money(r['totalEarnings'] as num?, currency: currency)),
      MapEntry('Vrijeme provedeno na stranici', Formatters.minutesToHoursAndMinutes(r['timeOnPlatformMinutes'] as num?)),
      MapEntry('Prosječna ocjena', '${r['averageRating'] ?? '-'}'),
    ];
  }

  Future<void> _export({required bool print}) async {
    final r = _report;
    if (r == null) return;
    setState(() => _exporting = true);
    try {
      final breakdown = (r['monthlyBreakdown'] as List<dynamic>? ?? const [])
          .map((e) => e as Map<String, dynamic>)
          .map((e) => [
                '${Formatters.monthName(e['month'] as int)} ${e['year']}',
                Formatters.money(e['earnings'] as num?),
                '${e['newSubscribers'] ?? 0}',
                Formatters.minutesToHoursAndMinutes(e['minutesOnPlatform'] as num?),
              ])
          .toList();
      final bytes = await PdfReport.buildDetailReport(
        title: 'Izvještaj o mentoru',
        subtitle: '${r['fullName'] ?? widget.fullName} — ${Formatters.monthName(_month ?? 1)} $_year',
        fields: _fields(r),
        breakdownHeaders: const ['Mjesec', 'Zarada', 'Novi pretplatnici', 'Vrijeme na platformi'],
        breakdownRows: breakdown,
      );
      if (print) {
        await PdfReport.print(bytes, 'izvjestaj-mentor-${widget.mentorProfileId}');
      } else {
        final savedUri = await PdfReport.save(bytes, 'izvjestaj-mentor-${widget.mentorProfileId}.pdf');
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
    return GbDialog(
      title: 'Izvještaj — ${widget.fullName}',
      width: 640,
      actions: _loading || _error != null
          ? null
          : [
              OutlinedButton.icon(
                onPressed: _exporting ? null : () => _export(print: false),
                icon: const Icon(Icons.download),
                label: const Text('PREUZMI PDF'),
              ),
              const SizedBox(width: 8),
              ElevatedButton.icon(
                onPressed: _exporting ? null : () => _export(print: true),
                icon: const Icon(Icons.print),
                label: const Text('PRINTAJ'),
              ),
            ],
      child: _loading
          ? const SizedBox(height: 200, child: Center(child: CircularProgressIndicator()))
          : _error != null
              ? SizedBox(height: 100, child: Center(child: Text(_error!, style: const TextStyle(color: AppColors.danger))))
              : _buildContent(_report!),
    );
  }

  Widget _buildContent(Map<String, dynamic> r) {
    final breakdown = (r['monthlyBreakdown'] as List<dynamic>? ?? const []).map((e) => e as Map<String, dynamic>).toList();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(children: [
          Expanded(
            child: DropdownButtonFormField<int>(
              initialValue: _month,
              decoration: const InputDecoration(labelText: 'Mjesec'),
              items: List.generate(12, (i) => i + 1)
                  .map((m) => DropdownMenuItem(value: m, child: Text(Formatters.monthName(m))))
                  .toList(),
              onChanged: (value) {
                setState(() => _month = value);
                _load();
              },
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: DropdownButtonFormField<int>(
              initialValue: _year,
              decoration: const InputDecoration(labelText: 'Godina'),
              items: List.generate(5, (i) => DateTime.now().year - i)
                  .map((y) => DropdownMenuItem(value: y, child: Text('$y')))
                  .toList(),
              onChanged: (value) {
                setState(() => _year = value);
                _load();
              },
            ),
          ),
        ]),
        const SizedBox(height: 16),
        Wrap(
          spacing: 12,
          runSpacing: 12,
          children: [
            StatTile(label: 'Aktivni pretplatnici', value: '${r['activeSubscribers'] ?? 0}'),
            StatTile(label: 'Mjesečna zarada', value: Formatters.money(r['monthlyEarnings'] as num?, currency: r['currency'] as String? ?? 'usd')),
            StatTile(label: 'Ukupna zarada', value: Formatters.money(r['totalEarnings'] as num?, currency: r['currency'] as String? ?? 'usd')),
            StatTile(label: 'Vrijeme na stranici', value: Formatters.minutesToHoursAndMinutes(r['timeOnPlatformMinutes'] as num?)),
          ],
        ),
        const SizedBox(height: 20),
        const Text('Pregled po mjesecima (zadnjih 6)', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold)),
        const SizedBox(height: 8),
        if (breakdown.isEmpty)
          const Padding(padding: EdgeInsets.all(12), child: Text('Nema podataka.', style: TextStyle(color: AppColors.textMuted)))
        else
          Table(
            border: TableBorder.all(color: Colors.white24),
            columnWidths: const {0: FlexColumnWidth(2), 1: FlexColumnWidth(2), 2: FlexColumnWidth(2), 3: FlexColumnWidth(2)},
            children: [
              const TableRow(
                decoration: BoxDecoration(color: AppColors.panelLight),
                children: [
                  Padding(padding: EdgeInsets.all(8), child: Text('Mjesec', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold))),
                  Padding(padding: EdgeInsets.all(8), child: Text('Zarada', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold))),
                  Padding(padding: EdgeInsets.all(8), child: Text('Novi pretplatnici', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold))),
                  Padding(padding: EdgeInsets.all(8), child: Text('Vrijeme', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold))),
                ],
              ),
              ...breakdown.map(
                (row) => TableRow(children: [
                  Padding(padding: const EdgeInsets.all(8), child: Text('${Formatters.monthName(row['month'] as int)} ${row['year']}', style: const TextStyle(color: Colors.white))),
                  Padding(padding: const EdgeInsets.all(8), child: Text(Formatters.money(row['earnings'] as num?), style: const TextStyle(color: Colors.white))),
                  Padding(padding: const EdgeInsets.all(8), child: Text('${row['newSubscribers'] ?? 0}', style: const TextStyle(color: Colors.white))),
                  Padding(padding: const EdgeInsets.all(8), child: Text(Formatters.minutesToHoursAndMinutes(row['minutesOnPlatform'] as num?), style: const TextStyle(color: Colors.white))),
                ]),
              ),
            ],
          ),
      ],
    );
  }
}
