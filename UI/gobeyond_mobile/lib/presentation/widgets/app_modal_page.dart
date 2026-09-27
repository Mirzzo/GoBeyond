import 'package:flutter/material.dart';

import '../../core/theme/app_theme.dart';

/// Full-screen modal shell (mentor detail, KUPI PLAN questionnaire, plan
/// full text, payment flow, subscription detail...) with the explicit
/// top-right "X" close control the course UI rules require on every
/// popup/form, plus the system back gesture (it is a normal pushed route).
class AppModalPage extends StatelessWidget {
  const AppModalPage({
    super.key,
    required this.body,
    this.title,
    this.actions,
    this.padding = const EdgeInsets.fromLTRB(20, 8, 20, 20),
  });

  final Widget body;
  final String? title;
  final List<Widget>? actions;
  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppTheme.background,
      appBar: AppBar(
        backgroundColor: AppTheme.background,
        automaticallyImplyLeading: false,
        title: title == null
            ? null
            : Text(title!, style: const TextStyle(fontWeight: FontWeight.w800)),
        actions: [
          ...?actions,
          IconButton(
            icon: const Icon(Icons.close_rounded, size: 28),
            tooltip: 'Zatvori',
            onPressed: () => Navigator.of(context).pop(),
          ),
        ],
      ),
      body: SafeArea(
        child: Padding(padding: padding, child: body),
      ),
    );
  }
}
