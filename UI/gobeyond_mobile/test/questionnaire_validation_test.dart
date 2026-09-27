import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/data/models/mentor_summary.dart';
import 'package:gobeyond_mobile/presentation/screens/mentor/questionnaire_screen.dart';

MentorDetail _mentor() => MentorDetail.fromJson({
      'mentorProfileId': 1,
      'fullName': 'Marko Marković',
      'trainingTypeId': 1,
      'trainingTypeName': 'Weightlifting',
      'averageRating': 4.5,
      'reviewCount': 10,
      'monthlyPrice': 19.99,
      'currency': 'USD',
      'yearsOfExperience': 5,
      'age': 30,
      'bio': 'Iskusan mentor.',
      'specializationNames': <String>[],
      'reviews': <Map<String, dynamic>>[],
    });

void main() {
  // The questionnaire renders all 6 questions in a scrollable ListView;
  // give the test surface enough height that every field is mounted at
  // once (a normal ListView virtualizes off-screen children), so they can
  // all be filled in without scrolling between each one.
  Future<void> useTallSurface(WidgetTester tester) async {
    await tester.binding.setSurfaceSize(const Size(400, 2600));
    addTearDown(() => tester.binding.setSurfaceSize(null));
  }

  testWidgets(
      'KUPI PLAN questionnaire rejects answers shorter than 2 characters and blocks navigation',
      (tester) async {
    await useTallSurface(tester);
    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: QuestionnaireScreen(mentor: _mentor()),
      ),
    );
    await tester.pumpAndSettle();

    // Fill every free-text answer with a single character (below the 2-char
    // minimum) and try to continue.
    final fields = find.byType(TextFormField);
    expect(fields, findsNWidgets(6));
    for (var i = 0; i < 6; i++) {
      await tester.enterText(fields.at(i), 'a');
    }

    await tester.ensureVisible(find.text('PRETPLATI SE 19.99\$'));
    await tester.tap(find.text('PRETPLATI SE 19.99\$'));
    await tester.pumpAndSettle();

    expect(
      find.text('Odgovor mora imati između 2 i 500 znakova.'),
      findsWidgets,
    );
    // Still on the questionnaire: the confirmation screen never opened.
    expect(find.text('KUPI PLAN'), findsOneWidget);
  });

  testWidgets(
      'valid answers on every question navigate to the subscription confirmation',
      (tester) async {
    await useTallSurface(tester);
    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: QuestionnaireScreen(mentor: _mentor()),
      ),
    );
    await tester.pumpAndSettle();

    final fields = find.byType(TextFormField);
    expect(fields, findsNWidgets(6));
    for (var i = 0; i < 6; i++) {
      await tester.enterText(fields.at(i), 'Ovo je validan odgovor broj $i.');
    }

    await tester.ensureVisible(find.text('PRETPLATI SE 19.99\$'));
    await tester.tap(find.text('PRETPLATI SE 19.99\$'));
    await tester.pumpAndSettle();

    expect(find.text('Potvrda pretplate'), findsOneWidget);
  });
}
