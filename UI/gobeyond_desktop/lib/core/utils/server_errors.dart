/// Bridges backend `errors.{field: [...]}` (400 responses) into
/// `TextFormField.validator`s, so a field-level backend error shows up under
/// the matching control exactly like a local validation error would.
///
/// Usage: `validator: _serverErrors.wrap('email', Validators.email)`, then
/// after a failed submit: `_serverErrors.apply(apiError.fieldErrors);
/// setState(() {}); _formKey.currentState?.validate();`
class ServerErrors {
  Map<String, String> _errors = const {};

  void apply(Map<String, String> errors) {
    _errors = errors;
  }

  void clear() {
    _errors = const {};
  }

  String? Function(String?) wrap(String field, String? Function(String?) local) {
    return (value) => forField(field) ?? local(value);
  }

  /// Looks up an exact key first (e.g. `bio`), then falls back to a
  /// dotted/nested variant (e.g. `mentor.bio`) in case the backend qualifies
  /// nested-object field names.
  String? forField(String field) {
    if (_errors.containsKey(field)) return _errors[field];
    final lowerField = field.toLowerCase();
    for (final entry in _errors.entries) {
      final key = entry.key.toLowerCase();
      if (key == lowerField || key.endsWith('.$lowerField')) {
        return entry.value;
      }
    }
    return null;
  }
}
