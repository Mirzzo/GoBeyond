import 'dart:typed_data';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_desktop/core/theme/app_theme.dart';
import 'package:gobeyond_desktop/core/utils/pdf_report.dart';
import 'package:gobeyond_desktop/presentation/screens/admin/widgets/client_report_dialog.dart';
import 'package:gobeyond_desktop/presentation/screens/admin/widgets/mentor_report_dialog.dart';
import 'package:plugin_platform_interface/plugin_platform_interface.dart';

import 'support/fake_api_interceptor.dart';

/// dsk-pdf-save-cancel-false-success: every PREUZMI PDF call site awaited
/// `PdfReport.save(...)` (which returns null when the user cancels the OS
/// Save dialog — nothing was written) and then showed "Izvještaj je
/// sačuvan." unconditionally, ignoring the returned Uri?.
class _StubFilePickerPlatform extends FilePickerPlatform with MockPlatformInterfaceMixin {
  _StubFilePickerPlatform(this._result);
  final Uri? _result;
  int saveFileCalls = 0;

  @override
  Future<Uri?> saveFile({
    required String fileName,
    required Uint8List bytes,
    required String mimeType,
    String? dialogTitle,
    String? initialDirectory,
    Function(FilePickerStatus)? onFileSaving,
    WindowsOptions windowsOptions = const WindowsOptions(),
    LinuxOptions linuxOptions = const LinuxOptions(),
    WebOptions webOptions = const WebOptions(),
  }) async {
    saveFileCalls++;
    return _result;
  }
}

void main() {
  final originalPlatform = FilePickerPlatform.instance;
  tearDown(() => FilePickerPlatform.instance = originalPlatform);

  group('PdfReport.save', () {
    test('returns null when the Save dialog is cancelled (windows_file_picker behaviour)', () async {
      FilePickerPlatform.instance = _StubFilePickerPlatform(null);
      final result = await PdfReport.save(Uint8List(0), 'izvjestaj.pdf');
      expect(result, isNull);
    });

    test('returns the saved Uri when the file was actually written', () async {
      final expected = Uri.file('C:/tmp/izvjestaj.pdf');
      FilePickerPlatform.instance = _StubFilePickerPlatform(expected);
      final result = await PdfReport.save(Uint8List(0), 'izvjestaj.pdf');
      expect(result, expected);
    });
  });

  // Drives the real MentorReportDialog/ClientReportDialog (not a copy of
  // their `if (mounted && uri != null) showSuccessSnack(...)` pattern) with
  // a fake backend through the ApiClient.instance.dio interceptor seam, so
  // the assertion actually exercises the production PREUZMI PDF button.
  Map<String, dynamic> mentorReport() => {
        'fullName': 'Dino Mentor',
        'trainingTypeName': 'Snaga',
        'activeSubscribers': 3,
        'totalSubscribers': 5,
        'monthlyEarnings': 149.97,
        'totalEarnings': 899.82,
        'timeOnPlatformMinutes': 120,
        'averageRating': 4.8,
        'currency': 'usd',
        'monthlyBreakdown': <dynamic>[],
      };

  Map<String, dynamic> clientReport() => {
        'fullName': 'Amina Klijent',
        'activeMentorName': 'Dino Mentor',
        'activeSubscriptions': 1,
        'totalSubscriptions': 2,
        'totalPaid': 259.90,
        'completedTrainings': 12,
        'progressEntries': 6,
        'lastProgressAt': '2026-09-01T00:00:00Z',
        'timeOnPlatformMinutes': 340,
        'currency': 'usd',
        'monthlyBreakdown': <dynamic>[],
      };

  group('MentorReportDialog PREUZMI PDF (real dialog, fake backend)', () {
    Widget buildApp() => MaterialApp(
          theme: AppTheme.dark,
          home: Scaffold(
            body: Builder(
              builder: (context) => ElevatedButton(
                onPressed: () => showMentorReportDialog(context, mentorProfileId: 7, fullName: 'Dino Mentor'),
                child: const Text('open'),
              ),
            ),
          ),
        );

    testWidgets('shows no success message when the Save dialog is cancelled', (tester) async {
      final api = FakeApiInterceptor.install();
      addTearDown(api.uninstall);
      api.on('GET', '/api/admin/reports/mentors/7', (options, handler) => handler.resolve(fakeResponse(options, mentorReport())));
      FilePickerPlatform.instance = _StubFilePickerPlatform(null);

      await tester.pumpWidget(buildApp());
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(OutlinedButton, 'PREUZMI PDF'));
      await tester.pumpAndSettle();

      expect(find.text('Izvještaj je sačuvan.'), findsNothing);
    });

    testWidgets('shows the success message when a file was actually saved', (tester) async {
      final api = FakeApiInterceptor.install();
      addTearDown(api.uninstall);
      api.on('GET', '/api/admin/reports/mentors/7', (options, handler) => handler.resolve(fakeResponse(options, mentorReport())));
      FilePickerPlatform.instance = _StubFilePickerPlatform(Uri.file('C:/tmp/izvjestaj-mentor.pdf'));

      await tester.pumpWidget(buildApp());
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(OutlinedButton, 'PREUZMI PDF'));
      await tester.pumpAndSettle();

      expect(find.text('Izvještaj je sačuvan.'), findsOneWidget);
    });
  });

  group('ClientReportDialog PREUZMI PDF (real dialog, fake backend)', () {
    Widget buildApp() => MaterialApp(
          theme: AppTheme.dark,
          home: Scaffold(
            body: Builder(
              builder: (context) => ElevatedButton(
                onPressed: () => showClientReportDialog(context, clientProfileId: 9, fullName: 'Amina Klijent'),
                child: const Text('open'),
              ),
            ),
          ),
        );

    testWidgets('shows no success message when the Save dialog is cancelled', (tester) async {
      final api = FakeApiInterceptor.install();
      addTearDown(api.uninstall);
      api.on('GET', '/api/admin/reports/clients/9', (options, handler) => handler.resolve(fakeResponse(options, clientReport())));
      FilePickerPlatform.instance = _StubFilePickerPlatform(null);

      await tester.pumpWidget(buildApp());
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(OutlinedButton, 'PREUZMI PDF'));
      await tester.pumpAndSettle();

      expect(find.text('Izvještaj je sačuvan.'), findsNothing);
    });

    testWidgets('shows the success message when a file was actually saved', (tester) async {
      final api = FakeApiInterceptor.install();
      addTearDown(api.uninstall);
      api.on('GET', '/api/admin/reports/clients/9', (options, handler) => handler.resolve(fakeResponse(options, clientReport())));
      FilePickerPlatform.instance = _StubFilePickerPlatform(Uri.file('C:/tmp/izvjestaj-klijent.pdf'));

      await tester.pumpWidget(buildApp());
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(OutlinedButton, 'PREUZMI PDF'));
      await tester.pumpAndSettle();

      expect(find.text('Izvještaj je sačuvan.'), findsOneWidget);
    });
  });
}
