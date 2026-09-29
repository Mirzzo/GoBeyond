import 'package:flutter/material.dart';

import '../../../../core/services/admin_service.dart';
import '../../../../core/utils/api_error.dart';
import '../../../../core/utils/server_errors.dart';
import '../../../../core/utils/validators.dart';
import '../../../widgets/dialogs.dart';

/// Admin "RESETUJ LOZINKU" — new + confirm only, the admin never needs the
/// user's old password.
///
/// The form is its own [StatefulWidget] so its [TextEditingController]s are
/// created in `initState` and disposed in `dispose` (called only once the
/// route is actually removed), instead of being disposed by hand right after
/// `await showDialog(...)` returns — which raced the dialog's exit
/// transition and threw "A TextEditingController was used after being
/// disposed." on every confirm/cancel.
Future<void> showResetPasswordDialog(BuildContext context, {required int userId, required String fullName}) {
  return showDialog<void>(
    context: context,
    builder: (_) => _ResetPasswordDialog(userId: userId, fullName: fullName),
  );
}

class _ResetPasswordDialog extends StatefulWidget {
  const _ResetPasswordDialog({required this.userId, required this.fullName});

  final int userId;
  final String fullName;

  @override
  State<_ResetPasswordDialog> createState() => _ResetPasswordDialogState();
}

class _ResetPasswordDialogState extends State<_ResetPasswordDialog> {
  final _formKey = GlobalKey<FormState>();
  final _serverErrors = ServerErrors();
  final _newPassword = TextEditingController();
  final _confirmPassword = TextEditingController();
  final _service = AdminService();

  @override
  void dispose() {
    _newPassword.dispose();
    _confirmPassword.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    try {
      final message = await _service.resetPassword(
        widget.userId,
        newPassword: _newPassword.text,
        confirmPassword: _confirmPassword.text,
      );
      if (!mounted) return;
      Navigator.of(context).pop();
      showSuccessSnack(context, message);
    } catch (error) {
      if (!mounted) return;
      final apiError = ApiError.from(error, fallback: 'Resetovanje lozinke nije uspjelo.');
      setState(() => _serverErrors.apply(apiError.fieldErrors));
      _formKey.currentState!.validate();
      showErrorSnack(context, apiError.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    return GbDialog(
      title: 'Resetuj lozinku — ${widget.fullName}',
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Odustani')),
        const SizedBox(width: 8),
        ElevatedButton(onPressed: _submit, child: const Text('RESETUJ LOZINKU')),
      ],
      child: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextFormField(
              controller: _newPassword,
              obscureText: true,
              decoration: const InputDecoration(labelText: 'Nova lozinka'),
              validator: _serverErrors.wrap('newPassword', Validators.password),
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _confirmPassword,
              obscureText: true,
              decoration: const InputDecoration(labelText: 'Potvrdite novu lozinku'),
              validator: _serverErrors.wrap('confirmPassword', (v) => Validators.confirmPassword(v, _newPassword.text)),
            ),
          ],
        ),
      ),
    );
  }
}
