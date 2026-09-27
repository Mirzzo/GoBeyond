import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:gobeyond_desktop/core/theme/app_theme.dart';
import 'package:gobeyond_desktop/presentation/screens/admin/widgets/report_tables.dart';

/// Regression test for the DataTable crash fixed in this round: both report
/// tables must build exactly one DataColumn per DataCell in every row
/// (headers + the trailing IZVJEŠTAJ action column). `DataColumn`/`DataRow`
/// are plain config objects, not widgets, so a column/cell-count mismatch
/// doesn't show up as "missing widgets" — it throws a `DataTable` assertion
/// during build instead, which `tester.takeException()` below would catch.
/// Pumping with fake data exercises the real construction path with no
/// backend involved.
void main() {
  final mentorItems = [
    {
      'mentorProfileId': 11,
      'fullName': 'Amir Hodžić',
      'trainingTypeName': 'Weightlifting',
      'activeSubscribers': 4,
      'totalSubscribers': 9,
      'monthlyEarnings': 320.0,
      'totalEarnings': 1450.0,
      'timeOnPlatformMinutes': 750,
      'averageRating': 4.8,
    },
    {
      'mentorProfileId': 12,
      'fullName': 'Lejla Kovač',
      'trainingTypeName': 'Calisthenics',
      'activeSubscribers': 2,
      'totalSubscribers': 3,
      'monthlyEarnings': 120.0,
      'totalEarnings': 300.0,
      'timeOnPlatformMinutes': 90,
      'averageRating': 5.0,
    },
  ];

  final clientItems = [
    {
      'clientProfileId': 21,
      'fullName': 'Mirza Rujanac',
      'activeMentorName': 'Amir Hodžić',
      'activeSubscriptions': 1,
      'totalSubscriptions': 2,
      'totalPaid': 199.98,
      'completedTrainings': 12,
      'progressEntries': 3,
      'timeOnPlatformMinutes': 500,
    },
  ];

  Widget wrap(Widget child) => MaterialApp(theme: AppTheme.dark, home: Scaffold(body: SingleChildScrollView(child: child)));

  testWidgets('MentorReportTable builds without a DataTable assertion crash', (tester) async {
    int? tappedId;
    String? tappedName;

    await tester.pumpWidget(wrap(MentorReportTable(
      items: mentorItems,
      totals: const {'mentorCount': 2, 'activeSubscribers': 6, 'monthlyEarnings': 440.0, 'totalEarnings': 1750.0, 'timeOnPlatformMinutes': 840},
      currency: 'BAM',
      onShowReport: (id, name) {
        tappedId = id;
        tappedName = name;
      },
    )));
    await tester.pumpAndSettle();

    // This is the actual regression check: mismatched columns.length vs.
    // cells.length throws inside DataTable.build, which would surface here.
    expect(tester.takeException(), isNull);

    for (final header in MentorReportTable.headers) {
      expect(find.text(header), findsOneWidget);
    }
    expect(find.text('Amir Hodžić'), findsOneWidget);
    expect(find.text('Lejla Kovač'), findsOneWidget);
    expect(find.text('UKUPNO (2 mentora)'), findsOneWidget);
    expect(find.widgetWithText(ElevatedButton, 'IZVJEŠTAJ'), findsNWidgets(mentorItems.length));

    // The table is wider than the test viewport (horizontally scrollable),
    // so invoke the button's callback directly rather than fighting the
    // off-screen hit-test — this still proves the row wired up the right
    // mentorProfileId/fullName to the tap handler.
    final button = tester.widget<ElevatedButton>(find.widgetWithText(ElevatedButton, 'IZVJEŠTAJ').first);
    button.onPressed!();
    expect(tappedId, 11);
    expect(tappedName, 'Amir Hodžić');
  });

  testWidgets('ClientReportTable builds without a DataTable assertion crash', (tester) async {
    int? tappedId;

    await tester.pumpWidget(wrap(ClientReportTable(
      items: clientItems,
      totals: const {'clientCount': 1, 'activeSubscriptions': 1, 'totalPaid': 199.98, 'completedTrainings': 12, 'timeOnPlatformMinutes': 500},
      currency: 'BAM',
      onShowReport: (id, name) => tappedId = id,
    )));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);

    for (final header in ClientReportTable.headers) {
      expect(find.text(header), findsOneWidget);
    }
    expect(find.text('Mirza Rujanac'), findsOneWidget);
    expect(find.text('UKUPNO (1 klijenata)'), findsOneWidget);
    expect(find.widgetWithText(ElevatedButton, 'IZVJEŠTAJ'), findsNWidgets(clientItems.length));

    final button = tester.widget<ElevatedButton>(find.widgetWithText(ElevatedButton, 'IZVJEŠTAJ').first);
    button.onPressed!();
    expect(tappedId, 21);
  });

  testWidgets('both tables build with an empty item list', (tester) async {
    await tester.pumpWidget(wrap(const MentorReportTable(items: [], totals: {}, currency: 'BAM', onShowReport: _noop2)));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);

    await tester.pumpWidget(wrap(const ClientReportTable(items: [], totals: {}, currency: 'BAM', onShowReport: _noop2)));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  });
}

void _noop2(int id, String name) {}
