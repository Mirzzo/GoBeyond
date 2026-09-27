import 'package:flutter/material.dart';

import '../../../../core/theme/app_theme.dart';
import '../../../../core/utils/formatters.dart';
import '../../../widgets/panel.dart';

/// The MENTORI IZVJEŠTAJ table + totals row (admin_dashboard_screen.dart).
///
/// Pulled out into its own widget (data in, callback out — no service calls)
/// so it can be pumped directly in a widget test with fake rows. This is
/// also what caught the "8 header columns but 9 DataCells per row" DataTable
/// crash: [headers] and the per-row cells must always stay in sync, which is
/// now enforced by building both from the same place.
class MentorReportTable extends StatelessWidget {
  const MentorReportTable({
    super.key,
    required this.items,
    required this.totals,
    required this.currency,
    required this.onShowReport,
  });

  final List<Map<String, dynamic>> items;
  final Map<String, dynamic> totals;
  final String currency;
  final void Function(int mentorProfileId, String fullName) onShowReport;

  static const headers = [
    'Mentor',
    'Vrsta treninga',
    'Aktivni pretpl.',
    'Ukupno pretpl.',
    'Mjesečna zarada',
    'Ukupna zarada',
    'Vrijeme na stranici',
    'Ocjena',
  ];

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: DataTable(
            columns: [
              ...headers.map((h) => DataColumn(label: Text(h))),
              const DataColumn(label: Text('')),
            ],
            rows: items
                .map((r) => DataRow(cells: [
                      DataCell(Text(r['fullName'] as String? ?? '')),
                      DataCell(Text(r['trainingTypeName'] as String? ?? '')),
                      DataCell(Text('${r['activeSubscribers'] ?? 0}')),
                      DataCell(Text('${r['totalSubscribers'] ?? 0}')),
                      DataCell(Text(Formatters.money(r['monthlyEarnings'] as num?, currency: currency))),
                      DataCell(Text(Formatters.money(r['totalEarnings'] as num?, currency: currency))),
                      DataCell(Text(Formatters.minutesToHoursAndMinutes(r['timeOnPlatformMinutes'] as num?))),
                      DataCell(Row(children: [const Icon(Icons.star, size: 14, color: AppColors.accent), Text(' ${r['averageRating'] ?? 0}')])),
                      DataCell(PillButton(
                        label: 'IZVJEŠTAJ',
                        dense: true,
                        onPressed: () => onShowReport(r['mentorProfileId'] as int, r['fullName'] as String? ?? ''),
                      )),
                    ]))
                .toList(),
          ),
        ),
        const SizedBox(height: 12),
        Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(12)),
          child: Wrap(spacing: 24, runSpacing: 8, children: [
            Text('UKUPNO (${totals['mentorCount'] ?? items.length} mentora)', style: const TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold)),
            Text('Aktivni pretplatnici: ${totals['activeSubscribers'] ?? 0}', style: const TextStyle(color: Colors.white)),
            Text('Mjesečna zarada: ${Formatters.money(totals['monthlyEarnings'] as num?, currency: currency)}', style: const TextStyle(color: Colors.white)),
            Text('Ukupna zarada: ${Formatters.money(totals['totalEarnings'] as num?, currency: currency)}', style: const TextStyle(color: Colors.white)),
            Text('Vrijeme na stranici: ${Formatters.minutesToHoursAndMinutes(totals['timeOnPlatformMinutes'] as num?)}', style: const TextStyle(color: Colors.white)),
          ]),
        ),
      ],
    );
  }
}

/// The KLIJENTI IZVJEŠTAJ table + totals row (admin_dashboard_screen.dart).
/// See [MentorReportTable] for why this is a standalone, data-in widget.
class ClientReportTable extends StatelessWidget {
  const ClientReportTable({
    super.key,
    required this.items,
    required this.totals,
    required this.currency,
    required this.onShowReport,
  });

  final List<Map<String, dynamic>> items;
  final Map<String, dynamic> totals;
  final String currency;
  final void Function(int clientProfileId, String fullName) onShowReport;

  static const headers = [
    'Klijent',
    'Aktivni mentor',
    'Aktivne pretpl.',
    'Ukupno pretpl.',
    'Ukupno plaćeno',
    'Treninzi',
    'Unosi napretka',
    'Vrijeme na stranici',
  ];

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: DataTable(
            columns: [
              ...headers.map((h) => DataColumn(label: Text(h))),
              const DataColumn(label: Text('')),
            ],
            rows: items
                .map((r) => DataRow(cells: [
                      DataCell(Text(r['fullName'] as String? ?? '')),
                      DataCell(Text(r['activeMentorName'] as String? ?? '-')),
                      DataCell(Text('${r['activeSubscriptions'] ?? 0}')),
                      DataCell(Text('${r['totalSubscriptions'] ?? 0}')),
                      DataCell(Text(Formatters.money(r['totalPaid'] as num?, currency: currency))),
                      DataCell(Text('${r['completedTrainings'] ?? 0}')),
                      DataCell(Text('${r['progressEntries'] ?? 0}')),
                      DataCell(Text(Formatters.minutesToHoursAndMinutes(r['timeOnPlatformMinutes'] as num?))),
                      DataCell(PillButton(
                        label: 'IZVJEŠTAJ',
                        dense: true,
                        onPressed: () => onShowReport(r['clientProfileId'] as int, r['fullName'] as String? ?? ''),
                      )),
                    ]))
                .toList(),
          ),
        ),
        const SizedBox(height: 12),
        Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(12)),
          child: Wrap(spacing: 24, runSpacing: 8, children: [
            Text('UKUPNO (${totals['clientCount'] ?? items.length} klijenata)', style: const TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold)),
            Text('Aktivne pretplate: ${totals['activeSubscriptions'] ?? 0}', style: const TextStyle(color: Colors.white)),
            Text('Ukupno plaćeno: ${Formatters.money(totals['totalPaid'] as num?, currency: currency)}', style: const TextStyle(color: Colors.white)),
            Text('Završeni treninzi: ${totals['completedTrainings'] ?? 0}', style: const TextStyle(color: Colors.white)),
            Text('Vrijeme na stranici: ${Formatters.minutesToHoursAndMinutes(totals['timeOnPlatformMinutes'] as num?)}', style: const TextStyle(color: Colors.white)),
          ]),
        ),
      ],
    );
  }
}
