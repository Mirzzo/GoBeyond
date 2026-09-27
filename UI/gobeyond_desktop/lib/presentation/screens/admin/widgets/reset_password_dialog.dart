import 'package:flutter/material.dart';

import '../../../../core/services/admin_service.dart';
import '../../../../core/utils/api_error.dart';
import '../../../../core/utils/server_errors.dart';
import '../../../../core/utils/validators.dart';
import '../../../widgets/dialogs.dart';

/// Admin "RESETUJ LOZINKU" — new + confirm only, the admin never needs the
/// user's old password.
Future<void> showResetPasswordDialog(BuildContext context, {required int userId, required String fullName}) async {
  final formKey = GlobalKey<FormState>();
  final serverErrors = ServerErrors();
  final newPassword = TextEditingController();
  final confirmPassword = TextEditingController();
  final service = AdminService();

  await showGbDialog<void>(
    context: context,
    title: 'Resetuj lozinku — $fullName',
    child: Form(
      key: formKey,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          TextFormField(
            controller: newPassword,
            obscureText: true,
            decoration: const InputDecoration(labelText: 'Nova lozinka'),
            validator: serverErrors.wrap('newPassword', Validators.password),
          ),
          const SizedBox(height: 14),
          TextFormField(
            controller: confirmPassword,
            obscureText: true,
            decoration: const InputDecoration(labelText: 'Potvrdite novu lozinku'),
            validator: serverErrors.wrap('confirmPassword', (v) => Validators.confirmPassword(v, newPassword.text)),
          ),
        ],
      ),
    ),
    actions: [
      TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Odustani')),
      const SizedBox(width: 8),
      ElevatedButton(
        onPressed: () async {
          if (!formKey.currentState!.validate()) return;
          try {
            final message = await service.resetPassword(
              userId,
              newPassword: newPassword.text,
              confirmPassword: confirmPassword.text,
            );
            if (!context.mounted) return;
            Navigator.of(context).pop();
            showSuccessSnack(context, message);
          } catch (error) {
            final apiError = ApiError.from(error, fallback: 'Resetovanje lozinke nije uspjelo.');
            serverErrors.apply(apiError.fieldErrors);
            formKey.currentState!.validate();
            if (!context.mounted) return;
            showErrorSnack(context, apiError.message);
          }
        },
        child: const Text('RESETUJ LOZINKU'),
      ),
    ],
  );
  newPassword.dispose();
  confirmPassword.dispose();
}
