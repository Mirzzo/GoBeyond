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
      subtitle: 'What should the next coaching block solve first?',
      options: ['Fat loss', 'Strength', 'Consistency', 'Technique'],
    ),
    _QuestionPrompt(
      title: 'Time commitment',
      subtitle: 'How much realistic time can you invest each week?',
      options: ['2 short sessions', '3 sessions', '4 sessions', '5+ sessions'],
    ),
    _QuestionPrompt(
      title: 'Health issues',
      subtitle: 'Choose the closest match for current limitations.',
      options: ['None', 'Minor joint pain', 'Recovery issues', 'Need modifications'],
    ),
    _QuestionPrompt(
      title: 'Medications',
      subtitle: 'Any regular medication or treatment to note?',
      options: ['None', 'Occasional pain relief', 'Daily prescription', 'Prefer to explain later'],
    ),
    _QuestionPrompt(
      title: 'Weekly availability',
      subtitle: 'When can you most reliably train?',
      options: ['Weekdays', 'Evenings', 'Weekends', 'Flexible'],
    ),
    _QuestionPrompt(
      title: 'Physical activity level',
      subtitle: 'How would you describe your current base level?',
      options: ['Beginner', 'Intermediate', 'Advanced', 'Returning after break'],
    ),
  ];

  void _submit() {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => SubscriptionScreen(
          mentor: widget.mentor,
          questionnaireAnswers: {
            'primaryGoal': _answers[0]!,
            'timeCommitment': _answers[1]!,
            'healthIssues': _answers[2]!,
            'medications': _answers[3]!,
            'weeklyAvailability': _answers[4]!,
            'physicalActivityLevel': _answers[5]!,
          },
        ),
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
                  'Answer all six prompts so the subscription flow can create a real onboarding request.',
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
