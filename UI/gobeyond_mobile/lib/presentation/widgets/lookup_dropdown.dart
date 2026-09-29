import 'package:flutter/material.dart';

import '../../core/theme/app_theme.dart';
import '../../core/utils/validators.dart';
import '../../data/models/lookup_item.dart';

/// A dropdown fed entirely from a reference-table API response (never a
/// hardcoded option list), per the course UI rules.
class LookupDropdown extends StatelessWidget {
  const LookupDropdown({
    super.key,
    required this.label,
    required this.items,
    required this.value,
    required this.onChanged,
    this.allowEmpty = false,
    this.emptyLabel = 'Nije bitno',
    this.errorText,
    this.requiredMessage,
  });

  final String label;
  final List<LookupItem> items;
  final int? value;
  final ValueChanged<int?> onChanged;
  final bool allowEmpty;
  final String emptyLabel;
  final String? errorText;

  /// Overrides the default "$label je obavezno." message (which reads wrong
  /// for a masculine/feminine label) with exact backend wording, e.g.
  /// "Odaberite spol."
  final String? requiredMessage;

  @override
  Widget build(BuildContext context) {
    final hasValue = value == null || items.any((item) => item.id == value);

    return DropdownButtonFormField<int?>(
      initialValue: hasValue ? value : null,
      isExpanded: true,
      decoration: InputDecoration(labelText: label, errorText: errorText),
      dropdownColor: AppTheme.panel,
      items: [
        if (allowEmpty)
          DropdownMenuItem<int?>(value: null, child: Text(emptyLabel)),
        ...items.map(
          (item) =>
              DropdownMenuItem<int?>(value: item.id, child: Text(item.name)),
        ),
      ],
      onChanged: onChanged,
      validator: allowEmpty
          ? null
          : (v) => v == null
              ? (requiredMessage ??
                  Validators.dropdownRequired(v, label: label))
              : null,
    );
  }
}
