import 'package:flutter/widgets.dart';

/// Mixin for forms that need to overlay backend `{errors: {field: [...]}}`
/// validation messages onto the same fields as the local validators, per the
/// contract's 400 response shape. Call [applyServerErrors] after a failed
/// submit, then the field's own `validator` should call [serverError] as a
/// fallback so both local and server messages render in the same place
/// (under the field, never in a dialog).
mixin ServerErrorsMixin<T extends StatefulWidget> on State<T> {
  Map<String, List<String>> _serverErrors = const {};

  /// Looks up [field] (e.g. `"weightKg"` or the exact nested key the backend
  /// uses, `"client.weightKg"`) in the last server error response. Falls
  /// back to matching any key that *ends with* `.field` so a screen doesn't
  /// silently swallow a nested error if it forgets (or gets wrong) the exact
  /// dotted prefix the backend nests validation errors under (`client.*`,
  /// `mentor.*`, `questionnaire.*` — see api-contract.md changelog v1.1).
  String? serverError(String field) {
    final direct = _serverErrors[field];
    if (direct != null && direct.isNotEmpty) return direct.first;

    for (final entry in _serverErrors.entries) {
      if (entry.key == field || entry.key.endsWith('.$field')) {
        if (entry.value.isNotEmpty) return entry.value.first;
      }
    }
    return null;
  }

  void applyServerErrors(
      Map<String, List<String>> errors, GlobalKey<FormState> formKey) {
    setState(() => _serverErrors = errors);
    // Re-run validators so the newly-set server errors show immediately.
    formKey.currentState?.validate();
  }

  void clearServerErrors() {
    if (_serverErrors.isNotEmpty) {
      setState(() => _serverErrors = const {});
    }
  }
}
