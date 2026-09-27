import 'package:flutter/material.dart';

import '../../widgets/app_modal_page.dart';

/// Mockup 11: the full, unmodified OPIS/ISHRANA text opened from "NASTAVI
/// ČITATI...." — original line breaks and emojis are preserved exactly as
/// the mentor wrote them.
class PlanFullTextScreen extends StatelessWidget {
  const PlanFullTextScreen(
      {super.key, required this.title, required this.text});

  final String title;
  final String text;

  @override
  Widget build(BuildContext context) {
    return AppModalPage(
      title: title,
      body: Scrollbar(
        child: SingleChildScrollView(
          child: Text(text, style: const TextStyle(height: 1.6, fontSize: 15)),
        ),
      ),
    );
  }
}
