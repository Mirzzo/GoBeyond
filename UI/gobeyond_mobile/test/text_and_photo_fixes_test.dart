import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/data/models/recommendation.dart';
import 'package:gobeyond_mobile/presentation/screens/home/home_screen.dart';
import 'package:gobeyond_mobile/presentation/screens/mentor/mentor_list_screen.dart';
import 'package:gobeyond_mobile/presentation/widgets/app_network_image.dart';

import 'support/fakes.dart';

void main() {
  testWidgets('MentorListScreen labels the sort control in Bosnian',
      (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: MentorListScreen(
          trainingTypeId: 1,
          trainingTypeName: 'Weightlifting',
          mentorRepository: FakeMentorRepository(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('SORTIRAJ PO:'), findsOneWidget);
    expect(find.text('SORT BY:'), findsNothing);
  });

  testWidgets(
      'Home screen recommendation card shows the mentor photo, not just a '
      'text initial', (tester) async {
    final mentorRepository = FakeMentorRepository()
      ..recommendedMentors = [
        MentorRecommendation.fromJson({
          'mentor': {
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
            'profileImageUrl': '/uploads/marko.png',
          },
          'score': 0.9,
          'reasons': <String>['Isti fitness cilj'],
        }),
      ];

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: HomeScreen(
          lookupRepository: FakeLookupRepository(),
          mentorRepository: mentorRepository,
        ),
      ),
    );
    // Not pumpAndSettle: CachedNetworkImage keeps an animated placeholder
    // ticking while its (test-environment-mocked) HTTP request resolves, so
    // this only pumps enough for the recommendation list itself to load.
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));

    expect(find.byType(AppNetworkImage), findsWidgets);
    // The old placeholder rendered the first letter of the name as text.
    expect(find.text('M'), findsNothing);
  });
}
