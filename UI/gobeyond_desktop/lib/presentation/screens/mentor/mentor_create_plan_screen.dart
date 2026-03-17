import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/services/panel_api_service.dart';
import '../../../core/session/session_controller.dart';
import '../../widgets/panel_card.dart';

class MentorCreatePlanScreen extends StatefulWidget {
  const MentorCreatePlanScreen({
    super.key,
    this.initialSubscriptionId,
  });

  final int? initialSubscriptionId;

  @override
  State<MentorCreatePlanScreen> createState() => _MentorCreatePlanScreenState();
}

class _MentorCreatePlanScreenState extends State<MentorCreatePlanScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final PanelApiService _service = PanelApiService(ApiClient());
  final TextEditingController _weekController = TextEditingController(text: '1');
  final TextEditingController _quoteController = TextEditingController();
  final List<_DayDraft> _days = [];
  List<Map<String, dynamic>> _requests = const [];
  int? _selectedSubscriptionId;
  bool _isLoading = true;
  bool _isSubmitting = false;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _resetDays();
    WidgetsBinding.instance.addPostFrameCallback((_) => _loadRequests());
  }

  @override
  void dispose() {
    _weekController.dispose();
    _quoteController.dispose();
    for (final day in _days) {
      day.dispose();
    }
    super.dispose();
  }

  void _resetDays() {
    for (final day in _days) {
      day.dispose();
    }
    _days
      ..clear()
      ..addAll([
        _DayDraft(dayOfWeek: 'Monday'),
        _DayDraft(dayOfWeek: 'Wednesday'),
        _DayDraft(dayOfWeek: 'Friday'),
      ]);
  }

  Future<void> _loadRequests() async {
    final session = context.read<SessionController>();

    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final requests = await session.runAuthenticated(
        (token) => _service.getCollaborationRequests(token),
      );
      if (!mounted) {
        return;
      }

      setState(() {
        _requests = requests;
        _selectedSubscriptionId = widget.initialSubscriptionId ??
            _selectedSubscriptionId ??
            (_requests.isEmpty ? null : _requests.first['subscriptionId'] as int);
      });
    } catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _errorMessage = error.toString();
      });
    } finally {
      if (mounted) {
        setState(() => _isLoading = false);
      }
    }
  }

  Future<void> _submit({required bool publishAfterSave}) async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    final subscriptionId = _selectedSubscriptionId;
    final session = context.read<SessionController>();
    if (subscriptionId == null) {
      return;
    }

    final payload = {
      'subscriptionId': subscriptionId,
      'weekNumber': int.parse(_weekController.text.trim()),
      'motivationalQuote': _quoteController.text.trim(),
      'days': _days.map((day) => day.toJson()).toList(),
    };

    setState(() => _isSubmitting = true);

    try {
      final createdPlan = await session.runAuthenticated(
        (token) => _service.createPlan(token, payload),
      );
      if (publishAfterSave) {
        await session.runAuthenticated(
          (token) => _service.publishPlan(token, createdPlan['id'] as int),
        );
      }

      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            publishAfterSave
                ? 'Training plan published successfully.'
                : 'Training plan saved as draft.',
          ),
        ),
      );

      _quoteController.clear();
      _weekController.text = '1';
      _resetDays();
      await _loadRequests();
    } catch (error) {
      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Unable to save plan: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _isSubmitting = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return PanelCard(
      title: 'Create Training Plan',
      description:
          'Mentors can now turn a collaboration request into a draft or published training plan from this screen.',
      actions: [
        IconButton(onPressed: _loadRequests, icon: const Icon(Icons.refresh_rounded)),
      ],
      child: _isLoading
          ? const Center(
              child: Padding(
                padding: EdgeInsets.all(32),
                child: CircularProgressIndicator(),
              ),
            )
          : _errorMessage != null
              ? Text(
                  _errorMessage!,
                  style: const TextStyle(color: Colors.redAccent),
                )
              : _requests.isEmpty
                  ? const Text('No collaboration requests are waiting for a plan.')
                  : Form(
                      key: _formKey,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          DropdownButtonFormField<int>(
                            initialValue: _selectedSubscriptionId,
                            items: _requests
                                .map(
                                  (request) => DropdownMenuItem<int>(
                                    value: request['subscriptionId'] as int,
                                    child: Text(
                                      '${request['clientName']} • ${(request['questionnaire'] as Map<String, dynamic>)['primaryGoal']}',
                                    ),
                                  ),
                                )
                                .toList(),
                            onChanged: _isSubmitting
                                ? null
                                : (value) {
                                    setState(() {
                                      _selectedSubscriptionId = value;
                                    });
                                  },
                            decoration: const InputDecoration(labelText: 'Collaboration request'),
                          ),
                          const SizedBox(height: 12),
                          Row(
                            children: [
                              Expanded(
                                child: TextFormField(
                                  controller: _weekController,
                                  keyboardType: TextInputType.number,
                                  decoration: const InputDecoration(labelText: 'Week number'),
                                  validator: (value) {
                                    final parsed = int.tryParse(value ?? '');
                                    if (parsed == null || parsed < 1 || parsed > 52) {
                                      return 'Use a week number between 1 and 52.';
                                    }
                                    return null;
                                  },
                                ),
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Text(
                                  'Publishing immediately notifies the client and removes the request from the pending list.',
                                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                                        color: const Color(0xFFBDBDBD),
                                      ),
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 12),
                          TextFormField(
                            controller: _quoteController,
                            minLines: 2,
                            maxLines: 3,
                            decoration: const InputDecoration(
                              labelText: 'Motivational quote',
                            ),
                            validator: (value) {
                              if ((value ?? '').trim().length < 6) {
                                return 'Enter at least 6 characters.';
                              }
                              return null;
                            },
                          ),
                          const SizedBox(height: 18),
                          ..._days.asMap().entries.map(
                            (entry) => Padding(
                              padding: const EdgeInsets.only(bottom: 14),
                              child: _DayEditor(
                                draft: entry.value,
                                canRemove: _days.length > 1,
                                onRemove: _isSubmitting
                                    ? null
                                    : () {
                                        setState(() {
                                          final removed = _days.removeAt(entry.key);
                                          removed.dispose();
                                        });
                                      },
                              ),
                            ),
                          ),
                          TextButton.icon(
                            onPressed: _isSubmitting
                                ? null
                                : () {
                                    setState(() {
                                      _days.add(_DayDraft(dayOfWeek: 'Saturday'));
                                    });
                                  },
                            icon: const Icon(Icons.add_rounded),
                            label: const Text('Add day'),
                          ),
                          const SizedBox(height: 12),
                          Row(
                            children: [
                              Expanded(
                                child: OutlinedButton(
                                  onPressed: _isSubmitting
                                      ? null
                                      : () => _submit(publishAfterSave: false),
                                  child: const Text('Save draft'),
                                ),
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: ElevatedButton(
                                  onPressed: _isSubmitting
                                      ? null
                                      : () => _submit(publishAfterSave: true),
                                  child: const Text('Publish plan'),
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
    );
  }
}

class _DayEditor extends StatelessWidget {
  const _DayEditor({
    required this.draft,
    required this.canRemove,
    required this.onRemove,
  });

  final _DayDraft draft;
  final bool canRemove;
  final VoidCallback? onRemove;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFF1F1F1F),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: const Color(0x25FFD700)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: DropdownButtonFormField<String>(
                  initialValue: draft.dayOfWeek,
                  items: _DayDraft.availableDays
                      .map(
                        (day) => DropdownMenuItem<String>(
                          value: day,
                          child: Text(day),
                        ),
                      )
                      .toList(),
                  onChanged: (value) {
                    if (value != null) {
                      draft.dayOfWeek = value;
                    }
                  },
                  decoration: const InputDecoration(labelText: 'Day'),
                ),
              ),
              if (canRemove) ...[
                const SizedBox(width: 12),
                IconButton(
                  onPressed: onRemove,
                  icon: const Icon(Icons.delete_outline_rounded),
                ),
              ],
            ],
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: TextFormField(
                  controller: draft.trainingDurationController,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(labelText: 'Training duration (min)'),
                  validator: _durationValidator,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: TextFormField(
                  controller: draft.nutritionDurationController,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(labelText: 'Nutrition duration (min)'),
                  validator: _durationValidator,
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          TextFormField(
            controller: draft.trainingDescriptionController,
            minLines: 2,
            maxLines: 3,
            decoration: const InputDecoration(labelText: 'Training description'),
            validator: _textValidator,
          ),
          const SizedBox(height: 12),
          TextFormField(
            controller: draft.nutritionDescriptionController,
            minLines: 2,
            maxLines: 3,
            decoration: const InputDecoration(labelText: 'Nutrition description'),
            validator: _textValidator,
          ),
        ],
      ),
    );
  }

  static String? _durationValidator(String? value) {
    final parsed = int.tryParse(value ?? '');
    if (parsed == null || parsed < 1 || parsed > 1440) {
      return 'Use 1-1440 minutes.';
    }
    return null;
  }

  static String? _textValidator(String? value) {
    if ((value ?? '').trim().length < 4) {
      return 'Enter at least 4 characters.';
    }
    return null;
  }
}

class _DayDraft {
  _DayDraft({required this.dayOfWeek})
      : trainingDurationController = TextEditingController(text: '45'),
        nutritionDurationController = TextEditingController(text: '15'),
        trainingDescriptionController = TextEditingController(),
        nutritionDescriptionController = TextEditingController();

  static const availableDays = <String>[
    'Monday',
    'Tuesday',
    'Wednesday',
    'Thursday',
    'Friday',
    'Saturday',
    'Sunday',
  ];

  String dayOfWeek;
  final TextEditingController trainingDurationController;
  final TextEditingController nutritionDurationController;
  final TextEditingController trainingDescriptionController;
  final TextEditingController nutritionDescriptionController;

  Map<String, dynamic> toJson() {
    return {
      'dayOfWeek': dayOfWeek,
      'trainingDurationMinutes': int.parse(trainingDurationController.text.trim()),
      'trainingDescription': trainingDescriptionController.text.trim(),
      'nutritionDurationMinutes': int.parse(nutritionDurationController.text.trim()),
      'nutritionDescription': nutritionDescriptionController.text.trim(),
    };
  }

  void dispose() {
    trainingDurationController.dispose();
    nutritionDurationController.dispose();
    trainingDescriptionController.dispose();
    nutritionDescriptionController.dispose();
  }
}
