import 'package:flutter/material.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/utils/validators.dart';
import '../../../data/models/training_plan.dart';
import '../../../data/models/training_session.dart';
import '../../../data/repositories/subscription_repository.dart';
import '../../../data/repositories/training_plan_repository.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/gb_scaffold.dart';
import '../../widgets/gobeyond_logo.dart';
import '../../widgets/primary_button.dart';
import '../../widgets/section_header.dart';
import '../../widgets/state_views.dart';
import '../../widgets/truncated_description.dart';
import '../home/home_screen.dart';
import '../subscription/subscription_screen.dart';
import 'plan_full_text_screen.dart';

enum _PlanState { hasPlan, awaitingMentor, preparingPlan, pendingPayment, none }

class _PlanScreenData {
  const _PlanScreenData(
      {required this.state, this.plan, this.sessions = const []});

  final _PlanState state;
  final TrainingPlan? plan;
  final List<TrainingSessionItem> sessions;
}

/// Mockups 11/12: motivational quote, day selector (defaults to today),
/// TRAJANJE + truncated OPIS/ISHRANA, "ZAVRŠIO SAM TRENING" logging, and the
/// empty states for every subscription lifecycle stage.
class MyPlanScreen extends StatefulWidget {
  const MyPlanScreen({
    super.key,
    this.trainingPlanRepository,
    this.subscriptionRepository,
  });

  final TrainingPlanRepository? trainingPlanRepository;
  final SubscriptionRepository? subscriptionRepository;

  @override
  State<MyPlanScreen> createState() => _MyPlanScreenState();
}

class _MyPlanScreenState extends State<MyPlanScreen> {
  late final TrainingPlanRepository _planRepository =
      widget.trainingPlanRepository ?? ApiTrainingPlanRepository();
  late final SubscriptionRepository _subscriptionRepository =
      widget.subscriptionRepository ?? ApiSubscriptionRepository();

  late Future<_PlanScreenData> _future;
  late int _selectedDay = DateTime.now().weekday; // 1=Mon..7=Sun, matches API.
  bool _showNutrition = false;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<_PlanScreenData> _load() async {
    try {
      final plan = await _planRepository.getMyCurrentPlan();
      final sessions = await _planRepository.getSessions(plan.id);
      return _PlanScreenData(
          state: _PlanState.hasPlan, plan: plan, sessions: sessions);
    } catch (error) {
      final apiError = ApiException.from(error);
      if (apiError.statusCode != 404) rethrow;

      final subscriptions = await _subscriptionRepository.getMySubscriptions();
      _PlanState state = _PlanState.none;
      if (subscriptions.any((s) => s.status == 'AwaitingMentor')) {
        state = _PlanState.awaitingMentor;
      } else if (subscriptions.any((s) => s.status == 'Active')) {
        state = _PlanState.preparingPlan;
      } else if (subscriptions.any((s) => s.status == 'PendingPayment')) {
        state = _PlanState.pendingPayment;
      }
      return _PlanScreenData(state: state);
    }
  }

  void _reload() => setState(() {
        _future = _load();
      });

  Future<void> _completeTraining(TrainingPlan plan, DayPlan day) async {
    final result = await showDialog<_CompletionResult>(
      context: context,
      builder: (_) => _CompleteTrainingDialog(dayName: day.dayName),
    );
    if (result == null) return;

    try {
      await _planRepository.completeSession(
        planId: plan.id,
        dayOfWeek: day.dayOfWeek,
        repetitions: result.repetitions,
        note: result.note,
      );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
            content: Text('Trening je uspješno evidentiran. Odličan posao!')),
      );
      _reload();
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(ApiException.from(error).message)),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return GbScaffold(
      body: FutureBuilder<_PlanScreenData>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const LoadingView();
          }
          if (snapshot.hasError) {
            return ErrorView(
              message: ApiException.from(snapshot.error!).message,
              onRetry: _reload,
            );
          }

          final data = snapshot.data!;
          switch (data.state) {
            case _PlanState.none:
              return EmptyStateView(
                message:
                    'Nemate aktivnu pretplatu. Odaberite mentora na početnoj stranici da započnete saradnju.',
                icon: Icons.assignment_outlined,
                actionLabel: 'IDI NA POČETNU',
                onAction: () => Navigator.of(context).pushAndRemoveUntil(
                  MaterialPageRoute(builder: (_) => const HomeScreen()),
                  (route) => false,
                ),
              );
            case _PlanState.pendingPayment:
              return EmptyStateView(
                message: 'Vaša pretplata čeka dovršetak plaćanja.',
                icon: Icons.payment_rounded,
                actionLabel: 'IDI NA PRETPLATU',
                onAction: () => Navigator.of(context).pushAndRemoveUntil(
                  MaterialPageRoute(builder: (_) => const SubscriptionScreen()),
                  (route) => false,
                ),
              );
            case _PlanState.awaitingMentor:
              return const EmptyStateView(
                message:
                    'Vaš zahtjev čeka prihvatanje mentora. Obavijestit ćemo vas čim mentor prihvati saradnju.',
                icon: Icons.hourglass_top_rounded,
              );
            case _PlanState.preparingPlan:
              return const EmptyStateView(
                message:
                    'Mentor priprema vaš personalizovani plan. Vratite se uskoro.',
                icon: Icons.edit_calendar_rounded,
              );
            case _PlanState.hasPlan:
              return _PlanContent(
                plan: data.plan!,
                sessions: data.sessions,
                selectedDay: _selectedDay,
                showNutrition: _showNutrition,
                onSelectDay: (day) => setState(() => _selectedDay = day),
                onToggleNutrition: (value) =>
                    setState(() => _showNutrition = value),
                onCompleteTraining: _completeTraining,
              );
          }
        },
      ),
    );
  }
}

class _PlanContent extends StatelessWidget {
  const _PlanContent({
    required this.plan,
    required this.sessions,
    required this.selectedDay,
    required this.showNutrition,
    required this.onSelectDay,
    required this.onToggleNutrition,
    required this.onCompleteTraining,
  });

  final TrainingPlan plan;
  final List<TrainingSessionItem> sessions;
  final int selectedDay;
  final bool showNutrition;
  final ValueChanged<int> onSelectDay;
  final ValueChanged<bool> onToggleNutrition;
  final void Function(TrainingPlan plan, DayPlan day) onCompleteTraining;

  @override
  Widget build(BuildContext context) {
    final day = plan.dayFor(selectedDay);
    final daySessions =
        sessions.where((s) => s.dayOfWeek == selectedDay).toList();

    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        AppPanel(
          child: Column(
            children: [
              const Center(child: GoBeyondLogo(fontSize: 26)),
              if (plan.motivationalQuote != null &&
                  plan.motivationalQuote!.isNotEmpty) ...[
                const SizedBox(height: 14),
                Text(
                  '"${plan.motivationalQuote}"',
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: AppTheme.accent,
                    fontStyle: FontStyle.italic,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
            ],
          ),
        ),
        if (plan.status != 'Published' || !plan.canEdit) ...[
          const SizedBox(height: 14),
          AppPanel(
            color: AppTheme.surface,
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            child: Row(
              children: [
                const Icon(Icons.info_outline_rounded,
                    color: AppTheme.accent, size: 20),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    !plan.canEdit
                        ? 'Ovaj plan je samo za pregled — saradnja s mentorom je prekinuta.'
                        : 'Plan je arhiviran. Mentor treba ponovo objaviti plan da biste mogli evidentirati treninge.',
                    style: const TextStyle(
                        color: AppTheme.textMuted, fontSize: 12.5),
                  ),
                ),
              ],
            ),
          ),
        ],
        const SizedBox(height: 16),
        Row(
          children: List.generate(7, (index) {
            final dayOfWeek = index + 1;
            final selected = dayOfWeek == selectedDay;
            final filled = plan.dayFor(dayOfWeek) != null;
            return Expanded(
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 3),
                child: GestureDetector(
                  onTap: () => onSelectDay(dayOfWeek),
                  child: Container(
                    padding: const EdgeInsets.symmetric(vertical: 10),
                    decoration: BoxDecoration(
                      color: selected ? AppTheme.accent : AppTheme.panelLight,
                      borderRadius: BorderRadius.circular(14),
                      border: filled && !selected
                          ? Border.all(color: AppTheme.accent, width: 1.4)
                          : null,
                    ),
                    child: Text(
                      Formatters.dayShort[index],
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        color: selected ? AppTheme.onAccent : Colors.white,
                        fontWeight: FontWeight.w800,
                        fontSize: 12,
                      ),
                    ),
                  ),
                ),
              ),
            );
          }),
        ),
        const SizedBox(height: 18),
        Container(
          padding: const EdgeInsets.symmetric(vertical: 12),
          decoration: BoxDecoration(
            color: AppTheme.accent,
            borderRadius: BorderRadius.circular(18),
          ),
          child: Text(
            'PLAN ZA ${Formatters.dayName(selectedDay).toUpperCase()}',
            textAlign: TextAlign.center,
            style: const TextStyle(
                color: AppTheme.onAccent, fontWeight: FontWeight.w800),
          ),
        ),
        const SizedBox(height: 16),
        if (day == null)
          const AppPanel(
            child: Text(
              'Mentor još nije popunio plan za ovaj dan.',
              textAlign: TextAlign.center,
              style: TextStyle(color: AppTheme.textMuted),
            ),
          )
        else ...[
          Row(
            children: [
              Expanded(
                child: _ToggleTab(
                  label: 'TRENING',
                  selected: !showNutrition,
                  onTap: () => onToggleNutrition(false),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: _ToggleTab(
                  label: 'ISHRANA',
                  selected: showNutrition,
                  onTap: () => onToggleNutrition(true),
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),
          AppPanel(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  'TRAJANJE: ${Formatters.durationFromMinutes(showNutrition ? (day.nutritionDurationMinutes ?? 0) : day.trainingDurationMinutes)}',
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                      color: AppTheme.accent,
                      fontWeight: FontWeight.w800,
                      fontSize: 17),
                ),
                const SizedBox(height: 16),
                const Text('OPIS',
                    textAlign: TextAlign.center,
                    style:
                        TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
                const SizedBox(height: 10),
                TruncatedDescription(
                  text: showNutrition
                      ? day.nutritionDescription
                      : day.trainingDescription,
                  onReadMore: () => Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => PlanFullTextScreen(
                        title:
                            'PLAN ZA ${Formatters.dayName(selectedDay).toUpperCase()} - ${showNutrition ? 'ISHRANA' : 'TRENING'}',
                        text: showNutrition
                            ? day.nutritionDescription
                            : day.trainingDescription,
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 20),
          PrimaryButton(
            label: 'ZAVRŠIO SAM TRENING',
            onPressed: plan.status == 'Published'
                ? () => onCompleteTraining(plan, day)
                : null,
          ),
          if (plan.status != 'Published')
            const Padding(
              padding: EdgeInsets.only(top: 8),
              child: Text(
                'Evidentiranje treninga je dostupno samo dok je plan objavljen.',
                textAlign: TextAlign.center,
                style: TextStyle(color: AppTheme.textMuted, fontSize: 12),
              ),
            ),
        ],
        const SizedBox(height: 26),
        SectionHeader(
            title:
                'MOJI TRENINZI ZA ${Formatters.dayName(selectedDay).toUpperCase()}'),
        const SizedBox(height: 12),
        if (daySessions.isEmpty)
          const Text('Još nema evidentiranih treninga za ovaj dan.',
              style: TextStyle(color: AppTheme.textMuted))
        else
          for (final session in daySessions)
            Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: AppPanel(
                padding:
                    const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                child: Row(
                  children: [
                    const Icon(Icons.check_circle_rounded,
                        color: AppTheme.success),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('${session.repetitions} ponavljanja',
                              style:
                                  const TextStyle(fontWeight: FontWeight.w700)),
                          Text(Formatters.dateTimeLabel(session.completedAt),
                              style: const TextStyle(
                                  color: AppTheme.textMuted, fontSize: 12)),
                          if (session.note != null && session.note!.isNotEmpty)
                            Padding(
                              padding: const EdgeInsets.only(top: 4),
                              child: Text(session.note!,
                                  style: const TextStyle(fontSize: 12.5)),
                            ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
      ],
    );
  }
}

class _ToggleTab extends StatelessWidget {
  const _ToggleTab(
      {required this.label, required this.selected, required this.onTap});

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 12),
        decoration: BoxDecoration(
          color: selected ? AppTheme.accent : AppTheme.panelLight,
          borderRadius: BorderRadius.circular(16),
        ),
        child: Text(
          label,
          textAlign: TextAlign.center,
          style: TextStyle(
            color: selected ? AppTheme.onAccent : Colors.white,
            fontWeight: FontWeight.w800,
          ),
        ),
      ),
    );
  }
}

class _CompletionResult {
  const _CompletionResult(this.repetitions, this.note);

  final int repetitions;
  final String? note;
}

class _CompleteTrainingDialog extends StatefulWidget {
  const _CompleteTrainingDialog({required this.dayName});

  final String dayName;

  @override
  State<_CompleteTrainingDialog> createState() =>
      _CompleteTrainingDialogState();
}

class _CompleteTrainingDialogState extends State<_CompleteTrainingDialog> {
  final _formKey = GlobalKey<FormState>();
  final _repetitionsController = TextEditingController();
  final _noteController = TextEditingController();

  @override
  void dispose() {
    _repetitionsController.dispose();
    _noteController.dispose();
    super.dispose();
  }

  void _submit() {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    Navigator.of(context).pop(
      _CompletionResult(
        int.parse(_repetitionsController.text.trim()),
        _noteController.text.trim().isEmpty
            ? null
            : _noteController.text.trim(),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Row(
        children: [
          Expanded(child: Text('Trening za ${widget.dayName}')),
          IconButton(
            icon: const Icon(Icons.close_rounded),
            onPressed: () => Navigator.of(context).pop(),
          ),
        ],
      ),
      content: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            TextFormField(
              controller: _repetitionsController,
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(labelText: 'Broj ponavljanja'),
              validator: (value) => Validators.numberRange(
                value,
                min: 1,
                max: 10000,
                label: 'Broj ponavljanja',
                isInt: true,
              ),
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _noteController,
              maxLines: 3,
              maxLength: 500,
              decoration:
                  const InputDecoration(labelText: 'Napomena (opciono)'),
              validator: (value) => Validators.textLength(
                value,
                min: 0,
                max: 500,
                label: 'Napomena',
                optional: true,
              ),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Odustani'),
        ),
        FilledButton(onPressed: _submit, child: const Text('POTVRDI')),
      ],
    );
  }
}
