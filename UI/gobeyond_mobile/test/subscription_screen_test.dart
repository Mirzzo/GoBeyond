import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/data/models/subscription.dart';
import 'package:gobeyond_mobile/presentation/screens/subscription/subscription_detail_screen.dart';
import 'package:gobeyond_mobile/presentation/screens/subscription/subscription_screen.dart';
import 'package:gobeyond_mobile/presentation/widgets/state_views.dart';

import 'support/fakes.dart';

Subscription _subscription({
  String status = 'Active',
  bool canReview = true,
  bool canRenew = true,
  bool canCancel = true,
  int? reviewId,
}) =>
    Subscription.fromJson({
      'id': 102,
      'mentorProfileId': 1,
      'mentorFullName': 'Marko Marković',
      'trainingTypeName': 'Weightlifting',
      'status': status,
      'price': 19.99,
      'currency': 'USD',
      'createdAt': DateTime.now().toIso8601String(),
      'canReview': canReview,
      'canRenew': canRenew,
      'canCancel': canCancel,
      'reviewId': reviewId,
    });

// 1080x2400 at 420 dpi (the emulator used for manual checks) in logical px.
const _phoneSize = Size(411, 914);

Future<void> _pumpScreen(
  WidgetTester tester,
  FakeSubscriptionRepository repository, {
  double textScale = 1.0,
}) async {
  await tester.binding.setSurfaceSize(_phoneSize);
  addTearDown(() => tester.binding.setSurfaceSize(null));
  await tester.pumpWidget(
    MaterialApp(
      theme: AppTheme.theme,
      builder: (context, child) => MediaQuery(
        data: MediaQuery.of(context)
            .copyWith(textScaler: TextScaler.linear(textScale)),
        child: child!,
      ),
      home: SubscriptionScreen(subscriptionRepository: repository),
    ),
  );
  await tester.pumpAndSettle();
}

/// The button (elevated or outlined) that shows [label].
Finder _button(String label) => find.ancestor(
      of: find.text(label),
      matching: find.byWidgetPredicate(
          (widget) => widget is ElevatedButton || widget is OutlinedButton),
    );

/// Every action is as wide as the card, starts at the same x, and its
/// label fits on one line.
void _expectFullWidthRows(WidgetTester tester, List<String> labels) {
  final rects = [for (final label in labels) tester.getRect(_button(label))];
  for (final rect in rects) {
    expect(rect.width, closeTo(rects.first.width, 0.5));
    expect(rect.left, closeTo(rects.first.left, 0.5));
  }
  // The card sits inside 20px list padding and 20px panel padding.
  expect(rects.first.width, closeTo(_phoneSize.width - 80, 0.5));
  for (var i = 1; i < rects.length; i++) {
    expect(rects[i].top, greaterThan(rects[i - 1].bottom));
  }
  for (final label in labels) {
    _expectSingleLine(tester, label);
  }
}

void _expectSingleLine(WidgetTester tester, String label) {
  final paragraph = tester.renderObject<RenderParagraph>(
      find.descendant(of: find.text(label), matching: find.byType(RichText)));
  final lineHeight =
      paragraph.getFullHeightForCaret(const TextPosition(offset: 0));
  expect(paragraph.size.height, lessThan(lineHeight * 1.5),
      reason: '"$label" wrapped onto a second line');
  expect(paragraph.didExceedMaxLines, isFalse,
      reason: '"$label" was truncated');
}

void main() {
  testWidgets('DETALJI on the current subscription card opens its detail',
      (tester) async {
    await _pumpScreen(
        tester, FakeSubscriptionRepository()..subscriptions = [_subscription()]);

    expect(find.text('DETALJI'), findsOneWidget);
    // SubscriptionDetailScreen uses the real API repository (same as every
    // other recursive navigation in this app), so its own FutureBuilder
    // never resolves in this offline test - pump a bounded duration
    // instead of pumpAndSettle, which would hang on its spinner.
    await tester.tap(find.text('DETALJI'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));

    expect(find.byType(SubscriptionDetailScreen), findsOneWidget);
  });

  testWidgets(
      'an Active subscription lists every action as an equal full-width row',
      (tester) async {
    await _pumpScreen(
      tester,
      FakeSubscriptionRepository()
        ..subscriptions = [_subscription(canReview: false, reviewId: 55)],
    );

    _expectFullWidthRows(tester, const [
      'PRODUŽI',
      'PORUKA MENTORU',
      'DETALJI',
      'UREDI RECENZIJU',
      'OBRIŠI RECENZIJU',
      'OTKAŽI PRETPLATU',
    ]);
  });

  testWidgets(
      'a PendingPayment subscription shows NASTAVI PLAĆANJE first and no '
      'PORUKA MENTORU', (tester) async {
    await _pumpScreen(
      tester,
      FakeSubscriptionRepository()
        ..subscriptions = [
          _subscription(
              status: 'PendingPayment', canReview: false, canRenew: false),
        ],
    );

    expect(find.text('PORUKA MENTORU'), findsNothing);
    _expectFullWidthRows(tester, const [
      'NASTAVI PLAĆANJE',
      'DETALJI',
      'OTKAŽI PRETPLATU',
    ]);
  });

  testWidgets('with a large system font the actions stay equal full-width '
      'rows without overflowing', (tester) async {
    await _pumpScreen(
      tester,
      FakeSubscriptionRepository()..subscriptions = [_subscription()],
      textScale: 1.3,
    );

    // A RenderFlex overflow would already have failed the pump above. (The
    // test font draws every glyph 1em wide, so label widths here are far
    // larger than with the real font and are not checked.)
    final labels = ['PRODUŽI', 'PORUKA MENTORU', 'DETALJI', 'NAPIŠI RECENZIJU'];
    final rects = [for (final label in labels) tester.getRect(_button(label))];
    for (final rect in rects) {
      expect(rect.width, closeTo(_phoneSize.width - 80, 0.5));
      expect(rect.height, greaterThanOrEqualTo(46));
    }
  });

  testWidgets('pull-to-refresh on Pretplata re-fetches the subscriptions',
      (tester) async {
    final subscriptionRepository = FakeSubscriptionRepository()
      ..subscriptions = [_subscription()];
    await _pumpScreen(tester, subscriptionRepository);

    final before = subscriptionRepository.getMySubscriptionsCalls;
    await tester.fling(
        find.text('TRENUTNA PRETPLATA'), const Offset(0, 300), 800);
    await tester.pumpAndSettle();

    expect(subscriptionRepository.getMySubscriptionsCalls, greaterThan(before));
  });

  testWidgets('pull-to-refresh keeps the subscriptions on screen while it '
      'reloads', (tester) async {
    final subscriptionRepository = FakeSubscriptionRepository()
      ..subscriptions = [_subscription()];
    await _pumpScreen(tester, subscriptionRepository);

    final gate = Completer<void>();
    subscriptionRepository.gate = gate;
    await tester.fling(
        find.text('TRENUTNA PRETPLATA'), const Offset(0, 300), 800);
    await tester.pump();
    await tester.pump(const Duration(seconds: 1));

    expect(subscriptionRepository.getMySubscriptionsCalls, 2);
    expect(find.byType(LoadingView), findsNothing);
    expect(find.text('Marko Marković'), findsOneWidget);
    expect(find.text('PRODUŽI'), findsOneWidget);

    gate.complete();
    await tester.pumpAndSettle();
    expect(find.text('Marko Marković'), findsOneWidget);
  });

  testWidgets('an empty subscription list is still pull-to-refreshable',
      (tester) async {
    final subscriptionRepository = FakeSubscriptionRepository();
    await _pumpScreen(tester, subscriptionRepository);

    final before = subscriptionRepository.getMySubscriptionsCalls;
    await tester.fling(find.byType(ListView), const Offset(0, 300), 800);
    await tester.pumpAndSettle();

    expect(subscriptionRepository.getMySubscriptionsCalls, greaterThan(before));
  });
}
