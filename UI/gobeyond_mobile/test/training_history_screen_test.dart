import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/data/models/progress_entry.dart';
import 'package:gobeyond_mobile/presentation/screens/progress/training_history_screen.dart';
import 'package:gobeyond_mobile/presentation/widgets/state_views.dart';

import 'support/fakes.dart';

// The fake only reports 2024/2025 as years with data, so every test pins the
// clock inside 2025 to make the selected year/month deterministic.
DateTime _june2025() => DateTime.utc(2025, 6, 15, 12);

ProgressEntryItem _entry({num weightKg = 80}) => ProgressEntryItem(
      id: 1,
      year: 2025,
      month: 6,
      monthName: 'Juni',
      weightKg: weightKg,
      measurements: 'Grudi 100cm',
      strength: 'Bench 80kg',
      conditioning: '5km u 25min',
      hasPlanSnapshot: false,
    );

Future<void> _pumpScreen(
  WidgetTester tester,
  FakeProgressRepository repository, {
  DateTime Function() clock = _june2025,
}) async {
  await tester.binding.setSurfaceSize(const Size(400, 2000));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  await tester.pumpWidget(
    MaterialApp(
      theme: AppTheme.theme,
      home: TrainingHistoryScreen(progressRepository: repository, clock: clock),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> _pickMonth(WidgetTester tester, String monthName) async {
  await tester.tap(find.textContaining('MJESEC:'));
  await tester.pumpAndSettle();
  await tester.tap(find.text(monthName).last);
  await tester.pumpAndSettle();
}

const _futureMonthMessage = 'Ne možete unositi podatke za budući mjesec.';

void main() {
  testWidgets(
      'the photo button is disabled until the month has a saved entry, with a hint',
      (tester) async {
    final repository = FakeProgressRepository();
    await _pumpScreen(tester, repository);

    final button = tester.widget<TextButton>(find.ancestor(
      of: find.text('Dodaj/promijeni sliku'),
      matching: find.byType(TextButton),
    ));
    expect(button.onPressed, isNull);
    expect(
      find.text('Sliku možete dodati nakon što sačuvate unos za ovaj mjesec.'),
      findsOneWidget,
    );
  });

  testWidgets('the photo button enables once the month has an entry',
      (tester) async {
    final repository = FakeProgressRepository();
    repository.entriesByKey['2025-6'] = _entry();
    await _pumpScreen(tester, repository);

    final button = tester.widget<TextButton>(find.ancestor(
      of: find.text('Dodaj/promijeni sliku'),
      matching: find.byType(TextButton),
    ));
    expect(button.onPressed, isNotNull);
  });

  testWidgets('saving an entry also refreshes the weight chart',
      (tester) async {
    final repository = FakeProgressRepository();
    await _pumpScreen(tester, repository);

    final chartCallsBefore = repository.getChartCalls;

    await tester.enterText(
        find.widgetWithText(TextFormField, 'Težina (kg)'), '80');
    await tester.enterText(
        find.widgetWithText(TextFormField, 'Obimi'), 'Grudi 100cm');
    await tester.enterText(
        find.widgetWithText(TextFormField, 'Snaga'), 'Bench 80kg');
    await tester.enterText(
        find.widgetWithText(TextFormField, 'Kondicija'), '5km u 25min');

    await tester.ensureVisible(find.text('SAČUVAJ UNOS'));
    await tester.tap(find.text('SAČUVAJ UNOS'));
    await tester.pumpAndSettle();

    expect(repository.entriesByKey.values.any((e) => e != null), isTrue);
    expect(repository.getChartCalls, greaterThan(chartCallsBefore));
  });

  testWidgets('saving an empty form shows the plural "Obimi su obavezni" '
      'message', (tester) async {
    final repository = FakeProgressRepository();
    await _pumpScreen(tester, repository);

    await tester.ensureVisible(find.text('SAČUVAJ UNOS'));
    await tester.tap(find.text('SAČUVAJ UNOS'));
    await tester.pumpAndSettle();

    expect(find.text('Obimi su obavezni (2–300 znakova).'), findsOneWidget);
    expect(find.text('Težina je obavezna.'), findsOneWidget);
    expect(find.text('Snaga je obavezna (2–300 znakova).'), findsOneWidget);
  });

  testWidgets('a month after the current UTC month is blocked as a future '
      'month, an earlier one is not', (tester) async {
    await _pumpScreen(tester, FakeProgressRepository());

    await _pickMonth(tester, 'Juli');
    expect(find.text(_futureMonthMessage), findsOneWidget);

    await _pickMonth(tester, 'Maj');
    expect(find.text(_futureMonthMessage), findsNothing);
  });

  // The backend compares against DateTime.UtcNow. Around midnight at the
  // turn of a month the device's local month can already (east of UTC) or
  // still (west of UTC) differ from the UTC month; the screen has to follow
  // the UTC month. Needs a non-UTC local time zone to tell the two apart.
  final boundary = DateTime.utc(2025, 7, 1);
  final offset = boundary.toLocal().timeZoneOffset;
  testWidgets(
    'near a month boundary the future-month check uses the UTC month, not '
    'the local one',
    (tester) async {
      final DateTime localNow;
      final bool julyIsFuture;
      if (offset > Duration.zero) {
        // Local time is already 1 July, UTC is still 30 June.
        localNow = boundary.subtract(const Duration(minutes: 15)).toLocal();
        julyIsFuture = true;
      } else {
        // Local time is still 30 June, UTC is already 1 July.
        localNow = boundary.add(const Duration(minutes: 15)).toLocal();
        julyIsFuture = false;
      }
      expect(localNow.isUtc, isFalse);
      expect(localNow.month, isNot(localNow.toUtc().month));

      await _pumpScreen(tester, FakeProgressRepository(),
          clock: () => localNow);

      await _pickMonth(tester, 'Juli');
      expect(find.text(_futureMonthMessage),
          julyIsFuture ? findsOneWidget : findsNothing);
    },
    skip: offset == Duration.zero,
  );

  testWidgets('pull-to-refresh keeps the page on screen while it reloads',
      (tester) async {
    final repository = FakeProgressRepository();
    repository.entriesByKey['2025-6'] = _entry();
    await _pumpScreen(tester, repository);

    final gate = Completer<void>();
    repository.gate = gate;
    await tester.fling(
        find.text('HISTORIJA TRENINGA'), const Offset(0, 800), 1000);
    await tester.pump();
    await tester.pump(const Duration(seconds: 1));

    expect(repository.getYearsCalls, 2);
    expect(find.byType(LoadingView), findsNothing);
    expect(find.text('HISTORIJA TRENINGA'), findsOneWidget);
    expect(find.text('Grudi 100cm'), findsOneWidget);

    gate.complete();
    await tester.pumpAndSettle();
    expect(find.text('Grudi 100cm'), findsOneWidget);
  });

  testWidgets('saving keeps the month card on screen while the page reloads, '
      'then shows the saved values', (tester) async {
    final repository = FakeProgressRepository();
    repository.entriesByKey['2025-6'] = _entry();
    await _pumpScreen(tester, repository);
    expect(find.text('SPREMI IZMJENE'), findsOneWidget);

    await tester.enterText(
        find.widgetWithText(TextFormField, 'Težina (kg)'), '78');
    final gate = Completer<void>();
    repository.gate = gate;
    await tester.ensureVisible(find.text('SPREMI IZMJENE'));
    await tester.tap(find.text('SPREMI IZMJENE'));
    await tester.pump();
    await tester.pump();

    expect(repository.entriesByKey['2025-6']!.weightKg, 78);
    expect(find.byType(LoadingView), findsNothing);
    expect(find.text('SPREMI IZMJENE'), findsOneWidget);
    expect(find.widgetWithText(TextFormField, '78'), findsOneWidget);

    gate.complete();
    await tester.pumpAndSettle();
    expect(find.byType(LoadingView), findsNothing);
    expect(find.widgetWithText(TextFormField, '78'), findsOneWidget);
  });

  testWidgets('switching from a month with an entry to an empty month does '
      'not carry the old values into the new month\'s form', (tester) async {
    final repository = FakeProgressRepository();
    repository.entriesByKey['2025-6'] = _entry(weightKg: 80);
    await _pumpScreen(tester, repository);
    expect(find.text('Grudi 100cm'), findsOneWidget);
    expect(find.text('SPREMI IZMJENE'), findsOneWidget);

    await tester.tap(find.textContaining('MJESEC:'));
    await tester.pumpAndSettle();
    // May's entry stays in flight until the gate opens.
    final gate = Completer<void>();
    repository.gate = gate;
    await tester.tap(find.text('Maj').last);
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));
    expect(repository.getEntryCalls.last, '2025-5');
    expect(find.text('Grudi 100cm'), findsNothing);

    gate.complete();
    await tester.pumpAndSettle();

    expect(find.textContaining('MJESEC: Maj'), findsOneWidget);
    expect(find.text('SAČUVAJ UNOS'), findsOneWidget);
    expect(find.text('Grudi 100cm'), findsNothing);
    expect(find.text('Bench 80kg'), findsNothing);
    expect(find.text('5km u 25min'), findsNothing);
    expect(find.widgetWithText(TextFormField, '80'), findsNothing);
  });

  testWidgets('a refreshed entry with new values updates the month card',
      (tester) async {
    final repository = FakeProgressRepository();
    repository.entriesByKey['2025-6'] = _entry(weightKg: 80);
    await _pumpScreen(tester, repository);
    expect(find.widgetWithText(TextFormField, '80'), findsOneWidget);

    // Changed elsewhere (e.g. on another device) and pulled in by a refresh.
    repository.entriesByKey['2025-6'] = _entry(weightKg: 77);
    await tester.fling(
        find.text('HISTORIJA TRENINGA'), const Offset(0, 800), 1000);
    await tester.pumpAndSettle();

    expect(find.widgetWithText(TextFormField, '77'), findsOneWidget);
  });
}
