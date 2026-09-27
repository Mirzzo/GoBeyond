import 'dart:typed_data';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/services.dart' show rootBundle;
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:printing/printing.dart';

/// Builds and exports Bosnian-diacritic-safe PDF reports.
///
/// Bundles Noto Sans (OFL licensed, assets/fonts) instead of relying on a
/// system font path, so č/ć/š/đ/ž render correctly on any machine.
class PdfReport {
  static pw.Font? _regular;
  static pw.Font? _bold;

  static Future<void> _ensureFonts() async {
    _regular ??= pw.Font.ttf(await rootBundle.load('assets/fonts/NotoSans-Regular.ttf'));
    _bold ??= pw.Font.ttf(await rootBundle.load('assets/fonts/NotoSans-Bold.ttf'));
  }

  /// Builds a titled table report. [rows] and [totalsRow] use the same
  /// column count as [headers].
  static Future<Uint8List> buildTableReport({
    required String title,
    String? subtitle,
    required List<String> headers,
    required List<List<String>> rows,
    List<String>? totalsRow,
  }) async {
    await _ensureFonts();
    final theme = pw.ThemeData.withFont(base: _regular!, bold: _bold!);
    final doc = pw.Document(theme: theme);

    doc.addPage(
      pw.MultiPage(
        pageFormat: PdfPageFormat.a4.landscape,
        margin: const pw.EdgeInsets.all(28),
        header: (context) => pw.Column(
          crossAxisAlignment: pw.CrossAxisAlignment.start,
          children: [
            pw.Text('GoBeyond', style: pw.TextStyle(font: _bold, fontSize: 12, color: PdfColors.grey700)),
            pw.SizedBox(height: 4),
            pw.Text(title, style: pw.TextStyle(font: _bold, fontSize: 18)),
            if (subtitle != null) pw.Text(subtitle, style: pw.TextStyle(font: _regular, fontSize: 11, color: PdfColors.grey700)),
            pw.SizedBox(height: 10),
            pw.Divider(),
          ],
        ),
        footer: (context) => pw.Align(
          alignment: pw.Alignment.centerRight,
          child: pw.Text(
            'Stranica ${context.pageNumber} / ${context.pagesCount}',
            style: pw.TextStyle(font: _regular, fontSize: 9, color: PdfColors.grey600),
          ),
        ),
        build: (context) => [
          pw.TableHelper.fromTextArray(
            headers: headers,
            data: rows,
            headerStyle: pw.TextStyle(font: _bold, fontSize: 9, color: PdfColors.white),
            headerDecoration: const pw.BoxDecoration(color: PdfColors.grey800),
            cellStyle: pw.TextStyle(font: _regular, fontSize: 9),
            cellAlignment: pw.Alignment.centerLeft,
            border: pw.TableBorder.all(color: PdfColors.grey400, width: 0.4),
            cellPadding: const pw.EdgeInsets.symmetric(horizontal: 6, vertical: 5),
          ),
          if (totalsRow != null) ...[
            pw.SizedBox(height: 10),
            pw.Container(
              padding: const pw.EdgeInsets.all(10),
              decoration: pw.BoxDecoration(color: PdfColors.grey200, borderRadius: pw.BorderRadius.circular(6)),
              child: pw.Row(
                mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
                children: totalsRow
                    .map((value) => pw.Text(value, style: pw.TextStyle(font: _bold, fontSize: 10)))
                    .toList(),
              ),
            ),
          ],
        ],
      ),
    );

    return doc.save();
  }

  /// Free-form report (used for the single-mentor/single-client detail PDF).
  static Future<Uint8List> buildDetailReport({
    required String title,
    required String subtitle,
    required List<MapEntry<String, String>> fields,
    List<String>? breakdownHeaders,
    List<List<String>>? breakdownRows,
  }) async {
    await _ensureFonts();
    final theme = pw.ThemeData.withFont(base: _regular!, bold: _bold!);
    final doc = pw.Document(theme: theme);

    doc.addPage(
      pw.MultiPage(
        pageFormat: PdfPageFormat.a4,
        margin: const pw.EdgeInsets.all(32),
        build: (context) => [
          pw.Text('GoBeyond', style: pw.TextStyle(font: _bold, fontSize: 12, color: PdfColors.grey700)),
          pw.SizedBox(height: 4),
          pw.Text(title, style: pw.TextStyle(font: _bold, fontSize: 20)),
          pw.Text(subtitle, style: pw.TextStyle(font: _regular, fontSize: 12, color: PdfColors.grey700)),
          pw.SizedBox(height: 16),
          pw.Divider(),
          pw.SizedBox(height: 8),
          pw.Table(
            columnWidths: const {0: pw.FlexColumnWidth(2), 1: pw.FlexColumnWidth(3)},
            children: fields
                .map(
                  (entry) => pw.TableRow(children: [
                    pw.Padding(
                      padding: const pw.EdgeInsets.symmetric(vertical: 5),
                      child: pw.Text(entry.key, style: pw.TextStyle(font: _bold, fontSize: 10)),
                    ),
                    pw.Padding(
                      padding: const pw.EdgeInsets.symmetric(vertical: 5),
                      child: pw.Text(entry.value, style: pw.TextStyle(font: _regular, fontSize: 10)),
                    ),
                  ]),
                )
                .toList(),
          ),
          if (breakdownHeaders != null && breakdownRows != null) ...[
            pw.SizedBox(height: 18),
            pw.Text('Pregled po mjesecima', style: pw.TextStyle(font: _bold, fontSize: 13)),
            pw.SizedBox(height: 8),
            pw.TableHelper.fromTextArray(
              headers: breakdownHeaders,
              data: breakdownRows,
              headerStyle: pw.TextStyle(font: _bold, fontSize: 9, color: PdfColors.white),
              headerDecoration: const pw.BoxDecoration(color: PdfColors.grey800),
              cellStyle: pw.TextStyle(font: _regular, fontSize: 9),
              border: pw.TableBorder.all(color: PdfColors.grey400, width: 0.4),
              cellPadding: const pw.EdgeInsets.symmetric(horizontal: 6, vertical: 5),
            ),
          ],
        ],
      ),
    );

    return doc.save();
  }

  /// Opens the OS "Save As" dialog and writes the given PDF bytes there.
  /// Returns the saved location, or null if the user cancelled.
  static Future<Uri?> save(Uint8List bytes, String suggestedFileName) async {
    return FilePicker.saveFile(
      dialogTitle: 'Sačuvaj izvještaj',
      fileName: suggestedFileName,
      type: FileType.custom,
      allowedExtensions: const ['pdf'],
      bytes: bytes,
    );
  }

  static Future<void> print(Uint8List bytes, String documentName) async {
    await Printing.layoutPdf(onLayout: (_) async => bytes, name: documentName);
  }
}
