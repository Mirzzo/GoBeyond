import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/utils/server_errors.dart';

/// Minimal host widget for [ServerErrorsMixin], standing in for a real form
/// screen (e.g. profile_screen.dart), whose `client.*`/`mentor.*`/
/// `questionnaire.*` nested field errors come back from the backend as
/// dotted keys per api-contract.md changelog v1.1.
class _FormHarness extends StatefulWidget {
  const _FormHarness();

  @override
  State<_FormHarness> createState() => _FormHarnessState();
}

class _FormHarnessState extends State<_FormHarness>
    with ServerErrorsMixin<_FormHarness> {
  final formKey = GlobalKey<FormState>();

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      home: Scaffold(
        body: Form(
          key: formKey,
          child: TextFormField(
            initialValue: 'x',
            // Mirrors profile_screen.dart's weightKg field: validated
            // against the exact dotted key the backend nests it under.
            validator: (_) => serverError('client.weightKg'),
          ),
        ),
      ),
    );
  }
}

void main() {
  testWidgets(
      'a nested "client.weightKg" server error surfaces under the matching field',
      (tester) async {
    await tester.pumpWidget(const _FormHarness());
    final state = tester.state<_FormHarnessState>(find.byType(_FormHarness));

    state.applyServerErrors(
      {
        'client.weightKg': ['Težina mora biti između 30 i 300 kg.'],
      },
      state.formKey,
    );
    await tester.pump();

    expect(find.text('Težina mora biti između 30 i 300 kg.'), findsOneWidget);
  });

  testWidgets(
      'serverError falls back to a dotted suffix match if a screen looks up the bare field name',
      (tester) async {
    await tester.pumpWidget(const _FormHarness());
    final state = tester.state<_FormHarnessState>(find.byType(_FormHarness));

    // Simulate a screen that (incorrectly) looks up the bare field name —
    // the mixin should still find it nested under "client.".
    expect(state.serverError('weightKg'), isNull); // nothing applied yet
    state.applyServerErrors(
      {
        'client.weightKg': ['Težina mora biti između 30 i 300 kg.'],
      },
      state.formKey,
    );
    expect(
        state.serverError('weightKg'), 'Težina mora biti između 30 i 300 kg.');

    state.clearServerErrors();
    expect(state.serverError('weightKg'), isNull);
  });
}
