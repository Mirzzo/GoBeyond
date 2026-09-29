import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/data/models/message_thread.dart';
import 'package:gobeyond_mobile/core/network/api_exception.dart';
import 'package:gobeyond_mobile/presentation/screens/messages/message_chat_screen.dart';
import 'package:gobeyond_mobile/presentation/widgets/state_views.dart';

import 'support/fakes.dart';

MessageItem _message(int id, String content, {bool isMine = false}) =>
    MessageItem(
      id: id,
      content: content,
      sentAt: DateTime.now().toIso8601String(),
      isMine: isMine,
      senderName: isMine ? 'Test Client' : 'Marko Marković',
    );

Future<void> _pumpChat(WidgetTester tester, FakeMessageRepository repository) =>
    tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: MessageChatScreen(
          subscriptionId: 1,
          otherPartyName: 'Marko Marković',
          messageRepository: repository,
        ),
      ),
    );

// Lets a just-completed fetch finish its setState and render it.
Future<void> _settle(WidgetTester tester) async {
  await tester.pump();
  await tester.pump();
}

Future<void> _send(WidgetTester tester, String text) async {
  await tester.enterText(find.byType(TextFormField), text);
  await tester.tap(find.byIcon(Icons.send_rounded));
  await tester.pump();
}

void main() {
  testWidgets(
      'MessageChatScreen silently polls every 10s and shows a message the '
      'other party sent while the chat stayed open', (tester) async {
    final repository = FakeMessageRepository();

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: MessageChatScreen(
          subscriptionId: 1,
          otherPartyName: 'Marko Marković',
          messageRepository: repository,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Još nema poruka. Napišite prvu poruku ispod.'),
        findsOneWidget);
    expect(repository.getThreadMessagesCalls, 1);

    // The other party sends a message while the chat is open (not via
    // _send(), which only the local user's button drives).
    repository.messages = [
      MessageItem(
        id: 99,
        content: 'Poruka od mentora',
        sentAt: DateTime.now().toIso8601String(),
        isMine: false,
        senderName: 'Marko Marković',
      ),
    ];

    // Nothing yet - the poll hasn't fired.
    await tester.pump(const Duration(seconds: 5));
    expect(find.text('Poruka od mentora'), findsNothing);

    // The 10s poll fires and silently picks up the new message.
    await tester.pump(const Duration(seconds: 5));
    await tester.pump();
    expect(find.text('Poruka od mentora'), findsOneWidget);
  });

  testWidgets('MessageChatScreen stops polling after dispose', (tester) async {
    final repository = FakeMessageRepository();

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.theme,
        home: Navigator(
          onGenerateRoute: (_) => MaterialPageRoute(
            builder: (_) => MessageChatScreen(
              subscriptionId: 1,
              otherPartyName: 'Marko Marković',
              messageRepository: repository,
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    // Replace the whole tree so MessageChatScreen (and its Timer) is
    // disposed; a leaked Timer would make pumpAndSettle below hang/throw.
    await tester.pumpWidget(const MaterialApp(home: SizedBox.shrink()));
    await tester.pump(const Duration(seconds: 30));
    await tester.pumpAndSettle();
  });

  testWidgets('polling pauses while the app is in the background and '
      'catches up immediately on resume', (tester) async {
    final repository = FakeMessageRepository();
    addTearDown(() => tester.binding
        .handleAppLifecycleStateChanged(AppLifecycleState.resumed));

    await _pumpChat(tester, repository);
    await tester.pumpAndSettle();
    expect(repository.getThreadMessagesCalls, 1);

    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.inactive);
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.hidden);
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.paused);
    await tester.pump(const Duration(seconds: 60));
    expect(repository.getThreadMessagesCalls, 1);

    repository.messages = [_message(7, 'Poruka dok je aplikacija u pozadini')];
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.hidden);
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.inactive);
    await tester.pump(const Duration(seconds: 20));
    expect(repository.getThreadMessagesCalls, 1);

    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.resumed);
    await _settle(tester);
    expect(repository.getThreadMessagesCalls, 2);
    expect(find.text('Poruka dok je aplikacija u pozadini'), findsOneWidget);

    // The regular 10s poll runs again after resuming.
    await tester.pump(const Duration(seconds: 10));
    expect(repository.getThreadMessagesCalls, 3);
  });

  testWidgets('a chat opened while the app is not in the foreground does '
      'not poll until the app resumes', (tester) async {
    final repository = FakeMessageRepository();
    addTearDown(() => tester.binding
        .handleAppLifecycleStateChanged(AppLifecycleState.resumed));
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.inactive);

    await _pumpChat(tester, repository);
    await tester.pump();
    await tester.pump(const Duration(seconds: 30));
    expect(repository.getThreadMessagesCalls, 1);

    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.resumed);
    await _settle(tester);
    expect(repository.getThreadMessagesCalls, 2);
    await tester.pump(const Duration(seconds: 10));
    expect(repository.getThreadMessagesCalls, 3);
  });

  testWidgets('a poll that started before a send cannot overwrite the list '
      'that already contains the sent message', (tester) async {
    final repository = FakeMessageRepository()
      ..messages = [_message(1, 'Dobar dan')];
    await _pumpChat(tester, repository);
    await tester.pumpAndSettle();

    repository.manualResponses = true;
    await tester.pump(const Duration(seconds: 10)); // poll -> pending[0]
    expect(repository.getThreadMessagesCalls, 2);

    await _send(tester, 'Zdravo!'); // reload after send -> pending[1]
    expect(repository.getThreadMessagesCalls, 3);

    repository.resolveResponse(
        1, [_message(1, 'Dobar dan'), _message(2, 'Zdravo!', isMine: true)]);
    await _settle(tester);
    expect(find.text('Zdravo!'), findsOneWidget);

    // The older poll answers last, with the list from before the send.
    repository.resolveResponse(0, [_message(1, 'Dobar dan')]);
    await _settle(tester);
    expect(find.text('Zdravo!'), findsOneWidget);
  });

  testWidgets('a newer poll that fails does not discard the reload after a '
      'send', (tester) async {
    final repository = FakeMessageRepository()
      ..messages = [_message(1, 'Dobar dan')];
    await _pumpChat(tester, repository);
    await tester.pumpAndSettle();

    repository.manualResponses = true;
    await _send(tester, 'Zdravo!'); // reload after send -> pending[0]
    await tester.pump(const Duration(seconds: 10)); // poll -> pending[1]
    expect(repository.getThreadMessagesCalls, 3);

    repository.failResponse(1, ApiException('Mreža nije dostupna.'));
    await _settle(tester);
    repository.resolveResponse(
        0, [_message(1, 'Dobar dan'), _message(2, 'Zdravo!', isMine: true)]);
    await _settle(tester);
    expect(find.text('Zdravo!'), findsOneWidget);
  });

  testWidgets('reloading after a send keeps the messages on screen instead '
      'of the loading view', (tester) async {
    final repository = FakeMessageRepository()
      ..messages = [_message(1, 'Dobar dan')];
    await _pumpChat(tester, repository);
    await tester.pumpAndSettle();

    repository.manualResponses = true;
    await _send(tester, 'Zdravo!');
    expect(repository.pendingResponses, hasLength(1));
    expect(find.byType(LoadingView), findsNothing);
    expect(find.text('Dobar dan'), findsOneWidget);

    repository.resolveResponse(
        0, [_message(1, 'Dobar dan'), _message(2, 'Zdravo!', isMine: true)]);
    await _settle(tester);
    expect(find.text('Zdravo!'), findsOneWidget);
  });
}
