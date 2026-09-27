import 'package:flutter/material.dart';

import '../../../../core/theme/app_theme.dart';
import '../../../widgets/avatar.dart';
import '../../../widgets/dialogs.dart';

/// PREGLED popup (prijava 3.1.3): client's physical condition, weight,
/// height, goal and the 6 questionnaire answers, so the mentor can build a
/// well-informed plan.
Future<T?> showClientDescriptionDialog<T>(
  BuildContext context,
  Map<String, dynamic> description, {
  List<Widget>? extraActions,
}) {
  final questionnaire = description['questionnaire'] as Map<String, dynamic>? ?? const {};

  return showGbDialog<T>(
    context: context,
    title: 'Opis klijenta — ${description['clientFullName'] ?? ''}',
    width: 600,
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(children: [
          GbAvatar(imageUrl: description['clientPhotoUrl'] as String?, size: 72),
          const SizedBox(width: 16),
          Expanded(
            child: Wrap(spacing: 20, runSpacing: 8, children: [
              _field('Godine', '${description['age'] ?? '-'}'),
              _field('Spol', description['genderName'] as String? ?? '-'),
              _field('Tjelesna težina', '${description['weightKg'] ?? '-'} kg'),
              _field('Visina', '${description['heightCm'] ?? '-'} cm'),
              _field('Nivo spreme', description['fitnessLevelName'] as String? ?? '-'),
              _field('Iskustvo', '${description['trainingExperienceYears'] ?? '-'} god.'),
            ]),
          ),
        ]),
        const Divider(height: 32, color: Colors.white24),
        const Text('Cilj', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold)),
        const SizedBox(height: 6),
        Text(description['fitnessGoalName'] as String? ?? '-', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600)),
        if ((description['goalDescription'] as String?)?.isNotEmpty == true) ...[
          const SizedBox(height: 4),
          Text(description['goalDescription'] as String, style: const TextStyle(color: Colors.white, height: 1.4)),
        ],
        const SizedBox(height: 20),
        const Text('Upitnik', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold)),
        const SizedBox(height: 8),
        _questionnaireRow('Glavni cilj', questionnaire['primaryGoal'] as String?),
        _questionnaireRow('Vremenska posvećenost', questionnaire['timeCommitment'] as String?),
        _questionnaireRow('Zdravstvene tegobe', questionnaire['healthIssues'] as String?),
        _questionnaireRow('Lijekovi/suplementi', questionnaire['medications'] as String?),
        _questionnaireRow('Broj sedmičnih treninga', questionnaire['weeklySessions'] as String?),
        _questionnaireRow('Ostale aktivnosti', questionnaire['outsideActivity'] as String?),
      ],
    ),
    actions: [
      TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('ZATVORI')),
      if (extraActions != null) ...[const SizedBox(width: 8), ...extraActions],
    ],
  );
}

Widget _field(String label, String value) {
  return SizedBox(
    width: 170,
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
        Text(value, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600)),
      ],
    ),
  );
}

Widget _questionnaireRow(String label, String? value) {
  return Padding(
    padding: const EdgeInsets.only(bottom: 10),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
        Text(value?.isNotEmpty == true ? value! : '-', style: const TextStyle(color: Colors.white)),
      ],
    ),
  );
}
