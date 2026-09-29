import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/auth/auth_controller.dart';
import 'package:gobeyond_mobile/core/auth/auth_scope.dart';
import 'package:gobeyond_mobile/core/theme/app_theme.dart';
import 'package:gobeyond_mobile/core/utils/date_of_birth_range.dart';
import 'package:gobeyond_mobile/presentation/screens/profile/profile_screen.dart';

import 'support/fakes.dart';

void main() {
  testWidgets(
      'a saved date of birth that is now out of range still opens the '
      'picker, at the earliest valid date', (tester) async {
    await tester.binding.setSurfaceSize(const Size(411, 2400));
    addTearDown(() => tester.binding.setSurfaceSize(null));

    // Valid when it was saved, older than the backend's limit today.
    final profileRepository = FakeProfileRepository()
      ..dateOfBirth = '1920-01-01';
    final authController = AuthController(
      authRepository: FakeAuthRepository(),
      profileRepository: profileRepository,
      activityRepository: FakeActivityRepository(),
    );
    await authController.refreshProfile();

    await tester.pumpWidget(
      AuthScope(
        controller: authController,
        child: MaterialApp(
          theme: AppTheme.theme,
          home: ProfileScreen(
            lookupRepository: FakeLookupRepository(),
            profileRepository: profileRepository,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('01.01.1920'), findsOneWidget);

    await tester.tap(find.text('01.01.1920'));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    final picker = tester.widget<DatePickerDialog>(find.byType(DatePickerDialog));
    final range = DateOfBirthRange(DateTime.now());
    expect(picker.initialDate, range.first);
    expect(picker.firstDate, range.first);
    expect(picker.lastDate, range.last);
  });
}
