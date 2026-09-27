import 'package:flutter/material.dart';

import '../../core/theme/app_theme.dart';

/// A dialog "card" with a title bar and the required top-right X close
/// button (course rule: every dialog must have an explicit close control).
class GbDialog extends StatelessWidget {
  const GbDialog({
    super.key,
    required this.title,
    required this.child,
    this.width = 560,
    this.actions,
    this.scrollable = true,
  });

  final String title;
  final Widget child;
  final double width;
  final List<Widget>? actions;
  final bool scrollable;

  @override
  Widget build(BuildContext context) {
    final content = ConstrainedBox(
      constraints: BoxConstraints(maxWidth: width, maxHeight: MediaQuery.of(context).size.height * 0.86),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            padding: const EdgeInsets.fromLTRB(20, 14, 12, 14),
            decoration: const BoxDecoration(
              color: AppColors.panelLight,
              borderRadius: BorderRadius.vertical(top: Radius.circular(18)),
            ),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    title,
                    style: const TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 18),
                  ),
                ),
                IconButton(
                  tooltip: 'Zatvori',
                  icon: const Icon(Icons.close, color: Colors.white),
                  onPressed: () => Navigator.of(context).pop(),
                ),
              ],
            ),
          ),
          Flexible(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: scrollable ? SingleChildScrollView(child: child) : child,
            ),
          ),
          if (actions != null && actions!.isNotEmpty)
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 0, 20, 20),
              child: Row(mainAxisAlignment: MainAxisAlignment.end, children: actions!),
            ),
        ],
      ),
    );

    return Dialog(
      backgroundColor: AppColors.panel,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
      child: content,
    );
  }
}

Future<T?> showGbDialog<T>({
  required BuildContext context,
  required String title,
  required Widget child,
  double width = 560,
  List<Widget>? actions,
  bool scrollable = true,
  bool barrierDismissible = true,
}) {
  return showDialog<T>(
    context: context,
    barrierDismissible: barrierDismissible,
    builder: (_) => GbDialog(title: title, width: width, actions: actions, scrollable: scrollable, child: child),
  );
}

/// Confirmation dialog required before every irreversible action
/// (delete/block/reject/cancel/publish/archive).
Future<bool> showConfirmDialog(
  BuildContext context, {
  required String title,
  required String message,
  String confirmLabel = 'Potvrdi',
  String cancelLabel = 'Odustani',
  bool danger = false,
}) async {
  final result = await showGbDialog<bool>(
    context: context,
    title: title,
    width: 460,
    child: Text(message, style: const TextStyle(color: Colors.white, fontSize: 15, height: 1.4)),
    actions: [
      TextButton(
        onPressed: () => Navigator.of(context).pop(false),
        child: Text(cancelLabel),
      ),
      const SizedBox(width: 8),
      ElevatedButton(
        style: danger
            ? ElevatedButton.styleFrom(backgroundColor: AppColors.danger, foregroundColor: Colors.white)
            : null,
        onPressed: () => Navigator.of(context).pop(true),
        child: Text(confirmLabel),
      ),
    ],
  );
  return result ?? false;
}

/// Prompts for a free-text reason within [minLength]..[maxLength] characters
/// (used by reject/cancel flows that require an explanation per the contract).
Future<String?> showReasonDialog(
  BuildContext context, {
  required String title,
  required String label,
  String? warning,
  int minLength = 10,
  int maxLength = 500,
  String confirmLabel = 'Potvrdi',
}) async {
  final controller = TextEditingController();
  final formKey = GlobalKey<FormState>();

  final result = await showGbDialog<String>(
    context: context,
    title: title,
    width: 520,
    child: Form(
      key: formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          if (warning != null) ...[
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: AppColors.danger.withValues(alpha: 0.15),
                borderRadius: BorderRadius.circular(10),
                border: Border.all(color: AppColors.danger.withValues(alpha: 0.4)),
              ),
              child: Row(
                children: [
                  const Icon(Icons.warning_amber_rounded, color: AppColors.danger),
                  const SizedBox(width: 10),
                  Expanded(child: Text(warning, style: const TextStyle(color: Colors.white))),
                ],
              ),
            ),
            const SizedBox(height: 16),
          ],
          TextFormField(
            controller: controller,
            maxLines: 4,
            maxLength: maxLength,
            decoration: InputDecoration(labelText: label),
            validator: (value) {
              final trimmed = value?.trim() ?? '';
              if (trimmed.length < minLength || trimmed.length > maxLength) {
                return 'Obrazloženje mora imati između $minLength i $maxLength znakova.';
              }
              return null;
            },
          ),
        ],
      ),
    ),
    actions: [
      TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Odustani')),
      const SizedBox(width: 8),
      ElevatedButton(
        onPressed: () {
          if (formKey.currentState!.validate()) {
            Navigator.of(context).pop(controller.text.trim());
          }
        },
        child: Text(confirmLabel),
      ),
    ],
  );
  controller.dispose();
  return result;
}

void showSuccessSnack(BuildContext context, String message) {
  ScaffoldMessenger.of(context).hideCurrentSnackBar();
  ScaffoldMessenger.of(context).showSnackBar(
    SnackBar(
      content: Row(children: [
        const Icon(Icons.check_circle, color: AppColors.success),
        const SizedBox(width: 10),
        Expanded(child: Text(message)),
      ]),
    ),
  );
}

void showErrorSnack(BuildContext context, String message) {
  ScaffoldMessenger.of(context).hideCurrentSnackBar();
  ScaffoldMessenger.of(context).showSnackBar(
    SnackBar(
      content: Row(children: [
        const Icon(Icons.error, color: AppColors.danger),
        const SizedBox(width: 10),
        Expanded(child: Text(message)),
      ]),
    ),
  );
}
