import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/utils/validators.dart';
import '../../../data/models/mentor_summary.dart';
import '../../../data/models/questionnaire.dart';
import '../../widgets/app_modal_page.dart';
import '../../widgets/primary_button.dart';
import 'subscription_confirm_screen.dart';

class _QuestionSpec {
  const _QuestionSpec(this.label, this.key);

  final String label;
  final String key;
}

const _questions = [
  _QuestionSpec(
    'Opišite vaš glavni cilj? (mršavljenje, povećanje mišićne mase, poboljšanje kondicije, snage, izdržljivosti...)',
    'primaryGoal',
  ),
  _QuestionSpec('Koliko vremena želite posvetiti ostvarivanju tog cilja?',
      'timeCommitment'),
  _QuestionSpec('Imate li zdravstvenih problema koji mogu utjecati na trening?',
      'healthIssues'),
  _QuestionSpec(
    'Da li ste trenutno pod terapijom ili lijekovima koji mogu utjecati na fizičku aktivnost?',
    'medications',
  ),
  _QuestionSpec(
      'Koliko puta sedmično realno možete trenirati?', 'weeklySessions'),
  _QuestionSpec(
    'Koliko često se baviš fizičkom aktivnošću van treninga (šetnje, sportovi, rekreacija)?',
    'outsideActivity',
  ),
];

/// Mockup 09: KUPI PLAN questionnaire — 6 free-text answers (2–500 chars)
/// that help the mentor build a personalized plan.
class QuestionnaireScreen extends StatefulWidget {
  const QuestionnaireScreen({super.key, required this.mentor});

  final MentorDetail mentor;

  @override
  State<QuestionnaireScreen> createState() => _QuestionnaireScreenState();
}

class _QuestionnaireScreenState extends State<QuestionnaireScreen> {
  final _formKey = GlobalKey<FormState>();
  final Map<String, TextEditingController> _controllers = {
    for (final q in _questions) q.key: TextEditingController(),
  };

  @override
  void dispose() {
    for (final controller in _controllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  void _submit() {
    if (!(_formKey.currentState?.validate() ?? false)) return;

    final questionnaire = Questionnaire(
      primaryGoal: _controllers['primaryGoal']!.text.trim(),
      timeCommitment: _controllers['timeCommitment']!.text.trim(),
      healthIssues: _controllers['healthIssues']!.text.trim(),
      medications: _controllers['medications']!.text.trim(),
      weeklySessions: _controllers['weeklySessions']!.text.trim(),
      outsideActivity: _controllers['outsideActivity']!.text.trim(),
    );

    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => SubscriptionConfirmScreen(
          mentor: widget.mentor,
          questionnaire: questionnaire,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    // Shared with KUPI PLAN, the confirmation screen, the subscription card
    // and payment history so the price reads identically everywhere
    // (previously this screen hand-rolled its own "29.00$" format instead of
    // Formatters.price's "$29.00").
    final priceLabel =
        Formatters.price(widget.mentor.monthlyPrice, widget.mentor.currency);

    return AppModalPage(
      body: Form(
        key: _formKey,
        autovalidateMode: AutovalidateMode.onUserInteraction,
        child: ListView(
          children: [
            Container(
              padding: const EdgeInsets.symmetric(vertical: 18),
              decoration: BoxDecoration(
                color: AppTheme.accent,
                borderRadius: BorderRadius.circular(24),
              ),
              child: const Text(
                'KUPI PLAN',
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: AppTheme.onAccent,
                  fontWeight: FontWeight.w900,
                  fontSize: 24,
                ),
              ),
            ),
            const SizedBox(height: 16),
            const Text(
              'Vaši odgovori na sljedeća pitanja pomažu mentoru da napravi što bolji personalizovani plan za vas.',
              style: TextStyle(fontWeight: FontWeight.w600),
            ),
            const SizedBox(height: 18),
            for (final question in _questions) ...[
              Text(question.label,
                  style: const TextStyle(fontWeight: FontWeight.w700)),
              const SizedBox(height: 8),
              TextFormField(
                controller: _controllers[question.key],
                minLines: 1,
                maxLines: 4,
                maxLength: 500,
                decoration: const InputDecoration(hintText: 'Vaš odgovor...'),
                validator: (value) => Validators.textLength(
                  value,
                  min: 2,
                  max: 500,
                  label: 'Odgovor',
                ),
              ),
              const SizedBox(height: 10),
            ],
            const SizedBox(height: 10),
            PrimaryButton(
                label: 'PRETPLATI SE $priceLabel', onPressed: _submit),
            const SizedBox(height: 12),
          ],
        ),
      ),
    );
  }
}
