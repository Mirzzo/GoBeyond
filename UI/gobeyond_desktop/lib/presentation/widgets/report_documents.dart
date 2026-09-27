import 'dart:io';

import 'package:flutter/material.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:printing/printing.dart';

class ReportDocuments extends StatelessWidget {
  const ReportDocuments({super.key, required this.title, required this.report, required this.fileName});
  final String title;
  final Map<String, dynamic> report;
  final String fileName;

  Future<List<int>> _createPdf() async {
    final fontFile = File(r'C:\Windows\Fonts\segoeui.ttf');
    final font = await fontFile.exists() ? pw.Font.ttf(await fontFile.readAsBytes()) : pw.Font.helvetica();
    final pdf = pw.Document();
    pdf.addPage(pw.MultiPage(build: (_) => [
      pw.Text(title, style: pw.TextStyle(font: font, fontSize: 22)),
      pw.SizedBox(height: 16),
      ...report.entries.where((e) => e.key != 'userId' && e.key != 'mentorId' && e.key != 'recentClients')
          .map((e) => pw.Padding(padding: const pw.EdgeInsets.only(bottom: 7),
            child: pw.Text('${_label(e.key)}: ${e.value ?? '-'}', style: pw.TextStyle(font: font)))),
      if (report['recentClients'] is List) ...[
        pw.SizedBox(height: 12),
        pw.Text('Recent clients', style: pw.TextStyle(font: font, fontSize: 16)),
        ...(report['recentClients'] as List).whereType<Map<String, dynamic>>().map((c) =>
          pw.Text('${c['clientName']} - ${c['goal']} (${c['status']})', style: pw.TextStyle(font: font))),
      ],
    ]));
    return pdf.save();
  }

  String _label(String key) => key.replaceAllMapped(RegExp(r'([A-Z])'), (m) => ' ${m[0]}').trim();

  Future<void> _handle(BuildContext context, bool print) async {
    try {
      final bytes = await _createPdf();
      if (print) {
        await Printing.layoutPdf(onLayout: (_) async => bytes);
      } else {
        final home = Platform.environment['USERPROFILE'] ?? Directory.current.path;
        final downloads = Directory('$home\\Downloads');
        await downloads.create(recursive: true);
        final file = File('${downloads.path}\\$fileName.pdf');
        await file.writeAsBytes(bytes, flush: true);
        if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Report saved to ${file.path}')));
      }
    } catch (e) { if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Report failed: $e'))); }
  }

  @override
  Widget build(BuildContext context) => Row(mainAxisSize: MainAxisSize.min, children: [
    TextButton(onPressed: () => _handle(context, false), child: const Text('Download PDF')),
    TextButton(onPressed: () => _handle(context, true), child: const Text('Print')),
  ]);
}
