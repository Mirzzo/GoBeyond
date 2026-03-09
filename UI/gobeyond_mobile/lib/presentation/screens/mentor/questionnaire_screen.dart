import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../data/models/mentor_model.dart';
import '../../widgets/app_panel.dart';
import '../subscription/subscription_screen.dart';

class QuestionnaireScreen extends StatefulWidget {
  const QuestionnaireScreen({super.key, this.mentor});

  final MentorModel? mentor;

  @override
  State<QuestionnaireScreen> createState() => _QuestionnaireScreenState();
}

class _QuestionnaireScreenState extends State<QuestionnaireScreen> {
  final Map<int, String> _answers = <int, String>{};

  static const _questions = <_QuestionPrompt>[
    _QuestionPrompt(
      title: 'Primary goal',
      subtitle: 'What should the next block solve first?',
      options: ['Fat loss', 'Strength', 'Consistency', 'Technique'],
    ),
    _QuestionPrompt(
      title: 'Training days',
      subtitle: 'How many realistic training days fit your calendar?',
      options: ['2 days', '3 days', '4 days', '5+ days'],
    ),
    _QuestionPrompt(
      title: 'Equipment access',
      subtitle: 'Choose the environment you can rely on every week.',
      options: ['Full gym', 'Minimal gym', 'Home setup', 'Bodyweight only'],
    ),
    _QuestionPrompt(
      title: 'Recovery level',
      subtitle: 'How recovered do you usually feel during the work week?',
      options: ['Low', 'Mixed', 'Mostly good', 'Very good'],
    ),
    _QuestionPrompt(
      title: 'Nutrition support',
      subtitle: 'How much structure do you want around food habits?',
      options: ['Minimal', 'Moderate', 'Detailed', 'Need full reset'],
    ),
    _QuestionPrompt(
      title: 'Coach communication',
      subtitle: 'How often do you expect feedback or adjustments?',
      options: ['Weekly', 'Twice weekly', 'As needed', 'Daily touchpoint'],
    ),
  ];

  void _submit() {
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Questionnaire captured. Opening subscription flow.')),
    );

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => SubscriptionScreen(mentor: widget.mentor),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final mentor = widget.mentor;
    final progress = _answers.length / _questions.length;

    return Scaffold(
      appBar: AppBar(title: const Text('Client questionnaire')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 8, 20, 24),
        children: [
          AppPanel(
            gradient: LinearGradient(
              colors: [
                AppTheme.secondaryColor.withValues(alpha: 0.22),
                AppTheme.surfaceColor,
              ],
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  mentor == null ? 'Plan fit questionnaire' : 'Questionnaire for ${mentor.name}',
                  style: Theme.of(context).textTheme.headlineMedium,
                ),
                const SizedBox(height: 10),
                Text(
                  'Answer all six prompts so the subscription flow can show a realistic coaching setup.',
                ),
                const SizedBox(height: 16),
                ClipRRect(
                  borderRadius: BorderRadius.circular(999),
                  child: LinearProgressIndicator(
                    value: progress,
                    minHeight: 10,
                    backgroundColor: Colors.white.withValues(alpha: 0.08),
                    color: AppTheme.secondaryColor,
                  ),
                ),
                const SizedBox(height: 10),
                Text(
                  '${_answers.length}/${_questions.length} answered',
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: AppTheme.textMutedColor,
                      ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 18),
          ..._questions.asMap().entries.map((entry) {
            final index = entry.key;
            final question = entry.value;

            return Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: AppPanel(
                color: AppTheme.surfaceColor,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '${index + 1}. ${question.title}',
                      style: Theme.of(context).textTheme.titleLarge,
                    ),
                    const SizedBox(height: 6),
                    Text(
                      question.subtitle,
                      style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                            color: AppTheme.textMutedColor,
                          ),
                    ),
                    const SizedBox(height: 14),
                    Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: question.options.map((option) {
                        return ChoiceChip(
                          label: Text(option),
                          selected: _answers[index] == option,
                          onSelected: (_) {
                            setState(() {
                              _answers[index] = option;
                            });
                          },
                        );
                      }).toList(),
                    ),
                  ],
                ),
              ),
            );
          }),
          const SizedBox(height: 8),
          SizedBox(
            width: double.infinity,
            child: ElevatedButton(
              onPressed: _answers.length == _questions.length ? _submit : null,
              child: const Text('Continue to subscription'),
            ),
          ),
        ],
      ),
    );
  }
}

class _QuestionPrompt {
  const _QuestionPrompt({
    required this.title,
    required this.subtitle,
    required this.options,
  });

  final String title;
  final String subtitle;
  final List<String> options;
}
