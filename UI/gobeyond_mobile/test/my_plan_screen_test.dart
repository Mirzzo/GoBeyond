import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/data/models/training_plan.dart';
import 'package:gobeyond_mobile/presentation/screens/plan/my_plan_screen.dart';
import 'package:gobeyond_mobile/presentation/widgets/state_views.dart';

import 'support/fakes.dart';

TrainingPlan _plan({required bool canEdit, String status = 'Published'}) =>
    TrainingPlan.fromJson({
      'id': 1,
      'subscriptionId': 1,
      'mentorFullName': 'Marko Marković',
      'clientFullName': 'Test Klijent',
      'status': status,
      'version': 1,
      'canEdit': canEdit,
      'days': [
        {
          'id': 1,
          'dayOfWeek': 3, // Wednesday
          'dayName': 'Srijeda',
          'trainingDurationMinutes': 60,
          'trainingDescription': 'Trening grudi i triceps.',
          'nutritionDescription': 'Visok unos proteina.',
        },
      ],
    });

void main() {
  Future<void> pump(WidgetTester tester, TrainingPlan plan) async {
    // The plan screen is a single long ListView (quote, day selector,
    // TRAJANJE/OPIS, ZAVRŠIO SAM TRENING + hint, MOJI TRENINZI); give the
    // test surface enough height that everything is mounted at once.
    await tester.binding.setSurfaceSize(const Size(400, 1600));
    addTearDown(() => tester.binding.setSurfaceSize(null));

    final repository = FakeTrainingPlanRepository()..plan = plan;
    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: MyPlanScreen(trainingPlanRepository: repository),
      ),
    );
    await tester.pumpAndSettle();
    // Wednesday is dayOfWeek 3; select it explicitly regardless of today.
    await tester.tap(find.text('SRI'));
    await tester.pumpAndSettle();
  }

  testWidgets(
      'day headers use the Bosnian accusative case after "ZA" (SRIJEDU, not SRIJEDA)',
      (tester) async {
    await pump(tester, _plan(canEdit: true));

    expect(find.text('PLAN ZA SRIJEDU'), findsOneWidget);
    expect(find.text('PLAN ZA SRIJEDA'), findsNothing);
    expect(find.text('MOJI TRENINZI ZA SRIJEDU'), findsOneWidget);
  });

  testWidgets(
      'ZAVRŠIO SAM TRENING is disabled when the plan is not editable (canEdit false)',
      (tester) async {
    await pump(tester, _plan(canEdit: false));

    final button = tester.widget<ElevatedButton>(find.ancestor(
      of: find.text('ZAVRŠIO SAM TRENING'),
      matching: find.byType(ElevatedButton),
    ));
    expect(button.onPressed, isNull);
    // canEdit is false for an expired as well as a cancelled collaboration,
    // so the hints use the backend's status-neutral wording.
    expect(
      find.text(
          'Ovaj plan je samo za pregled — saradnja s mentorom je završena.'),
      findsOneWidget,
    );
    expect(
      find.text('Saradnja je završena, pa se treninzi više ne mogu evidentirati.'),
      findsOneWidget,
    );
    expect(find.textContaining('prekinuta'), findsNothing);
  });

  testWidgets(
      'ZAVRŠIO SAM TRENING stays enabled for a published, editable plan',
      (tester) async {
    await pump(tester, _plan(canEdit: true));

    final button = tester.widget<ElevatedButton>(find.ancestor(
      of: find.text('ZAVRŠIO SAM TRENING'),
      matching: find.byType(ElevatedButton),
    ));
    expect(button.onPressed, isNotNull);
  });

  testWidgets('pull-to-refresh on Moj plan re-fetches the plan',
      (tester) async {
    final repository = FakeTrainingPlanRepository()
      ..plan = _plan(canEdit: true);

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: MyPlanScreen(trainingPlanRepository: repository),
      ),
    );
    await tester.pumpAndSettle();

    final before = repository.getMyCurrentPlanCalls;
    await tester.fling(find.byType(ListView), const Offset(0, 300), 800);
    await tester.pumpAndSettle();

    expect(repository.getMyCurrentPlanCalls, greaterThan(before));
  });

  testWidgets('pull-to-refresh keeps the plan on screen while it reloads',
      (tester) async {
    await tester.binding.setSurfaceSize(const Size(400, 1600));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    final repository = FakeTrainingPlanRepository()
      ..plan = _plan(canEdit: true);

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: MyPlanScreen(trainingPlanRepository: repository),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('SRI'));
    await tester.pumpAndSettle();
    expect(find.text('PLAN ZA SRIJEDU'), findsOneWidget);

    final gate = Completer<void>();
    repository.gate = gate;
    await tester.fling(find.byType(ListView), const Offset(0, 500), 800);
    await tester.pump();
    await tester.pump(const Duration(seconds: 1));

    expect(repository.getMyCurrentPlanCalls, 2);
    expect(find.byType(LoadingView), findsNothing);
    expect(find.text('PLAN ZA SRIJEDU'), findsOneWidget);

    gate.complete();
    await tester.pumpAndSettle();
    expect(find.text('PLAN ZA SRIJEDU'), findsOneWidget);
  });
}
