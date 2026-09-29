import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/data/models/mentor_summary.dart';
import 'package:gobeyond_mobile/data/models/subscription.dart';
import 'package:gobeyond_mobile/presentation/screens/mentor/mentor_detail_screen.dart';

import 'support/fakes.dart';

/// Records push/replace calls so the test can assert on the navigation
/// stack *shape* without depending on the pushed screen's own network calls
/// (it uses the real API repositories, like every other recursive
/// navigation in this app) or transition-animation timing.
class _RecordingNavigatorObserver extends NavigatorObserver {
  final List<String> events = [];

  @override
  void didPush(Route route, Route? previousRoute) => events.add('push');

  @override
  void didReplace({Route? newRoute, Route? oldRoute}) => events.add('replace');

  @override
  void didPop(Route route, Route? previousRoute) => events.add('pop');
}

MentorDetail _detail(int id, String name, {int reviewCount = 10}) =>
    MentorDetail.fromJson({
      'mentorProfileId': id,
      'fullName': name,
      'trainingTypeId': 1,
      'trainingTypeName': 'Weightlifting',
      'averageRating': 4.5,
      'reviewCount': reviewCount,
      'monthlyPrice': 19.99,
      'currency': 'USD',
      'yearsOfExperience': 5,
      'age': 30,
      'bio': 'Iskusan mentor.',
      'specializationNames': <String>[],
      'reviews': <Map<String, dynamic>>[],
    });

Subscription _subscription(String status) => Subscription.fromJson({
      'id': 1,
      'mentorProfileId': 99,
      'mentorFullName': 'Neko Drugi',
      'trainingTypeName': 'Weightlifting',
      'status': status,
      'price': 19.99,
      'currency': 'USD',
      'createdAt': DateTime.now().toIso8601String(),
      'canReview': false,
      'canRenew': false,
      'canCancel': true,
    });

void main() {
  // MentorDetailScreen is a single long ListView (photo, bio, reviews,
  // similar mentors, blocking text); give the test surface enough height
  // that everything is mounted at once instead of needing manual scrolling.
  Future<void> useTallSurface(WidgetTester tester) async {
    await tester.binding.setSurfaceSize(const Size(400, 1600));
    addTearDown(() => tester.binding.setSurfaceSize(null));
  }

  testWidgets(
      'tapping a similar mentor pushes (not replaces), so Back returns to '
      'the original mentor detail', (tester) async {
    await useTallSurface(tester);
    final mentorRepository = FakeMentorRepository()
      ..mentorsById = {
        1: _detail(1, 'Dino Prvi'),
        2: _detail(2, 'Haris Drugi'),
      }
      ..similarMentors = [
        MentorSummary.fromJson({
          'mentorProfileId': 2,
          'fullName': 'Haris Drugi',
          'trainingTypeId': 1,
          'trainingTypeName': 'Weightlifting',
          'averageRating': 4.0,
          'reviewCount': 3,
          'monthlyPrice': 19.99,
          'currency': 'USD',
          'yearsOfExperience': 2,
          'age': 25,
        }),
      ];
    final subscriptionRepository = FakeSubscriptionRepository();
    final observer = _RecordingNavigatorObserver();

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        navigatorObservers: [observer],
        home: MentorDetailScreen(
          mentorProfileId: 1,
          mentorRepository: mentorRepository,
          subscriptionRepository: subscriptionRepository,
        ),
      ),
    );
    await tester.pumpAndSettle();
    observer.events.clear();

    expect(find.text('IME: DINO PRVI'), findsOneWidget);

    // Deliberately never pumps a frame after the tap: the pushed screen
    // uses the real API repositories (same as every other recursive
    // navigation in this app, e.g. subscription history ->
    // SubscriptionDetailScreen), so building it would fire a real network
    // call this offline test can't let complete. `Navigator.push` records
    // the route (and notifies observers) synchronously in the `onTap`
    // handler, before any frame is drawn, so the push-vs-replace shape is
    // already observable without ever building that screen.
    await tester.tap(find.text('Haris Drugi'));

    // The regression: pushReplacement would report 'replace' and destroy
    // Dino's route; push keeps it on the stack underneath the new one.
    expect(observer.events, ['push']);
  });

  testWidgets('review count uses the Bosnian plural form', (tester) async {
    await useTallSurface(tester);
    final mentorRepository = FakeMentorRepository()
      ..mentorsById = {1: _detail(1, 'Marko Marković', reviewCount: 2)};

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: MentorDetailScreen(
          mentorProfileId: 1,
          mentorRepository: mentorRepository,
          subscriptionRepository: FakeSubscriptionRepository(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.textContaining('2 recenzije'), findsOneWidget);
    expect(find.textContaining('2 recenzija'), findsNothing);
  });

  testWidgets(
      'an AwaitingMentor blocking subscription shows the "still waiting" '
      'text, not the cancel-a-paid-subscription text', (tester) async {
    await useTallSurface(tester);
    final mentorRepository = FakeMentorRepository()
      ..mentorsById = {1: _detail(1, 'Marko Marković')};
    final subscriptionRepository = FakeSubscriptionRepository()
      ..subscriptions = [_subscription('AwaitingMentor')];

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: MentorDetailScreen(
          mentorProfileId: 1,
          mentorRepository: mentorRepository,
          subscriptionRepository: subscriptionRepository,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.textContaining('još čeka odgovor'), findsOneWidget);
    expect(find.textContaining('Otkažite postojeću pretplatu'), findsNothing);
  });

  testWidgets('an Active blocking subscription keeps the cancel-first text',
      (tester) async {
    await useTallSurface(tester);
    final mentorRepository = FakeMentorRepository()
      ..mentorsById = {1: _detail(1, 'Marko Marković')};
    final subscriptionRepository = FakeSubscriptionRepository()
      ..subscriptions = [_subscription('Active')];

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: MentorDetailScreen(
          mentorProfileId: 1,
          mentorRepository: mentorRepository,
          subscriptionRepository: subscriptionRepository,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.textContaining('Otkažite postojeću pretplatu'), findsOneWidget);
    expect(find.textContaining('još čeka odgovor'), findsNothing);
  });
}
