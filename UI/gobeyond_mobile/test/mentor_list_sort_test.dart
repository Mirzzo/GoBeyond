import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/data/models/mentor_summary.dart';
import 'package:gobeyond_mobile/presentation/screens/mentor/mentor_list_screen.dart';

import 'support/fakes.dart';

void main() {
  testWidgets(
      'mentor list requests mentors sorted by rating desc by default, '
      'then re-fetches when SORT BY and direction change', (tester) async {
    final repository = FakeMentorRepository();

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: MentorListScreen(
          trainingTypeId: 1,
          trainingTypeName: 'Weightlifting',
          mentorRepository: repository,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(repository.calls, hasLength(1));
    expect(repository.calls.last['trainingTypeId'], 1);
    expect(repository.calls.last['sortBy'], 'rating');
    expect(repository.calls.last['sortDirection'], 'desc');

    // Change SORT BY to Cijena (price).
    await tester.tap(find.text('Recenzije'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cijena').last);
    await tester.pumpAndSettle();

    expect(repository.calls.last['sortBy'], 'price');
    expect(repository.calls.last['sortDirection'], 'desc');

    // Flip sort direction to ascending.
    await tester.tap(find.byIcon(Icons.arrow_downward_rounded));
    await tester.pumpAndSettle();

    expect(repository.calls.last['sortBy'], 'price');
    expect(repository.calls.last['sortDirection'], 'asc');
  });

  testWidgets('mentor list shows an empty state when no mentors match',
      (tester) async {
    final repository = _EmptyMentorRepository();

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: MentorListScreen(
          trainingTypeId: 2,
          trainingTypeName: 'Calisthenics',
          mentorRepository: repository,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Nema mentora koji odgovaraju pretrazi.'), findsOneWidget);
  });
}

class _EmptyMentorRepository extends FakeMentorRepository {
  @override
  Future<List<MentorSummary>> getMentors({
    int? trainingTypeId,
    String? search,
    String sortBy = 'rating',
    String sortDirection = 'desc',
  }) async {
    calls.add({
      'trainingTypeId': trainingTypeId,
      'search': search,
      'sortBy': sortBy,
      'sortDirection': sortDirection,
    });
    return <MentorSummary>[];
  }
}
