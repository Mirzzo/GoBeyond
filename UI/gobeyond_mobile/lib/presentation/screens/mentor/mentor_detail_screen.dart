import 'package:flutter/material.dart';

import '../../../core/network/dio_client.dart';
import '../../../core/theme/app_theme.dart';
import '../../../data/models/mentor_model.dart';
import '../../../data/repositories/mentor_repository.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/section_header.dart';
import '../subscription/subscription_screen.dart';
import 'questionnaire_screen.dart';

class MentorDetailScreen extends StatefulWidget {
  const MentorDetailScreen({super.key, required this.mentor});

  final MentorModel mentor;

  @override
  State<MentorDetailScreen> createState() => _MentorDetailScreenState();
}

class _MentorDetailScreenState extends State<MentorDetailScreen> {
  final MentorRepository _repository = MentorRepository(DioClient());
  List<Map<String, dynamic>> _reviews = const [];
  bool _isLoadingReviews = true;

  Future<void> _writeReview() async {
    final formKey = GlobalKey<FormState>();
    final comment = TextEditingController();
    int rating = 5;
    final submitted = await showDialog<bool>(
            context: context,
            builder: (dialogContext) => StatefulBuilder(
                  builder: (context, setDialogState) => AlertDialog(
                    title: const Text('Ocijenite saradnju'),
                    content: Form(
                        key: formKey,
                        child:
                            Column(mainAxisSize: MainAxisSize.min, children: [
                          DropdownButtonFormField<int>(
                            initialValue: rating,
                            decoration:
                                const InputDecoration(labelText: 'Ocjena'),
                            items: [1, 2, 3, 4, 5]
                                .map((value) => DropdownMenuItem(
                                    value: value, child: Text('$value / 5')))
                                .toList(),
                            onChanged: (value) =>
                                setDialogState(() => rating = value ?? 5),
                          ),
                          TextFormField(
                            controller: comment,
                            maxLines: 3,
                            maxLength: 500,
                            decoration:
                                const InputDecoration(labelText: 'Komentar'),
                            validator: (value) =>
                                (value ?? '').trim().length < 4
                                    ? 'Unesite najmanje 4 znaka.'
                                    : null,
                          ),
                        ])),
                    actions: [
                      TextButton(
                          onPressed: () => Navigator.pop(dialogContext, false),
                          child: const Text('Odustani')),
                      ElevatedButton(
                          onPressed: () {
                            if (formKey.currentState!.validate())
                              Navigator.pop(dialogContext, true);
                          },
                          child: const Text('Objavi')),
                    ],
                  ),
                )) ??
        false;
    if (submitted && mounted) {
      try {
        await _repository.saveReview(
            widget.mentor.id, rating, comment.text.trim());
        await _loadReviews();
        if (mounted)
          ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(content: Text('Recenzija je sačuvana.')));
      } catch (error) {
        if (mounted)
          ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(content: Text('Recenzija nije sačuvana: $error')));
      }
    }
    comment.dispose();
  }

  @override
  void initState() {
    super.initState();
    _loadReviews();
  }

  Future<void> _loadReviews() async {
    try {
      final reviews = await _repository.getMentorReviews(widget.mentor.id);
      if (!mounted) {
        return;
      }

      setState(() {
        _reviews = reviews;
      });
    } catch (_) {
      if (!mounted) {
        return;
      }

      setState(() {
        _reviews = const [];
      });
    } finally {
      if (mounted) {
        setState(() => _isLoadingReviews = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final mentor = widget.mentor;
    final accentColor = Color(mentor.accentColorValue);

    return Scaffold(
      appBar: AppBar(title: const Text('Mentor profile')),
      body: CustomScrollView(
        slivers: [
          SliverPadding(
            padding: const EdgeInsets.fromLTRB(20, 8, 20, 24),
            sliver: SliverList(
              delegate: SliverChildListDelegate([
                AppPanel(
                  gradient: LinearGradient(
                    colors: [
                      accentColor.withValues(alpha: 0.28),
                      AppTheme.surfaceColor,
                    ],
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          CircleAvatar(
                            radius: 32,
                            backgroundColor:
                                accentColor.withValues(alpha: 0.18),
                            child: Text(
                              mentor.name
                                  .split(' ')
                                  .where((part) => part.isNotEmpty)
                                  .map((part) => part[0])
                                  .take(2)
                                  .join(),
                              style: TextStyle(
                                color: accentColor,
                                fontSize: 22,
                                fontWeight: FontWeight.w800,
                              ),
                            ),
                          ),
                          const SizedBox(width: 16),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  mentor.name,
                                  style: Theme.of(context)
                                      .textTheme
                                      .headlineMedium,
                                ),
                                const SizedBox(height: 6),
                                Text(
                                  '${mentor.category} | ${mentor.city}',
                                  style: Theme.of(context)
                                      .textTheme
                                      .bodyMedium
                                      ?.copyWith(
                                        color: AppTheme.textMutedColor,
                                      ),
                                ),
                              ],
                            ),
                          ),
                          Container(
                            padding: const EdgeInsets.symmetric(
                                horizontal: 12, vertical: 8),
                            decoration: BoxDecoration(
                              color: Colors.white.withValues(alpha: 0.08),
                              borderRadius: BorderRadius.circular(16),
                            ),
                            child: Text(
                              '\$${mentor.price.toStringAsFixed(0)}/mo',
                              style: Theme.of(context).textTheme.titleMedium,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 18),
                      Text(mentor.headline,
                          style: Theme.of(context).textTheme.titleMedium),
                      const SizedBox(height: 12),
                      Text(mentor.about),
                      const SizedBox(height: 18),
                      Wrap(
                        spacing: 10,
                        runSpacing: 10,
                        children: [
                          _TagStat(
                              label: mentor.nextStartLabel,
                              accentColor: accentColor),
                          _TagStat(
                              label: mentor.responseTimeLabel,
                              accentColor: accentColor),
                          _TagStat(
                            label: '${mentor.activeClients} active clients',
                            accentColor: accentColor,
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 24),
                const SectionHeader(
                  title: 'Specialties',
                  subtitle: 'Areas this mentor actively programs and reviews.',
                ),
                const SizedBox(height: 12),
                Wrap(
                  spacing: 10,
                  runSpacing: 10,
                  children: mentor.specialties
                      .map(
                        (specialty) => Chip(
                          label: Text(specialty),
                          avatar: Icon(Icons.check_rounded,
                              color: accentColor, size: 18),
                        ),
                      )
                      .toList(),
                ),
                const SizedBox(height: 24),
                const SectionHeader(
                  title: 'Client feedback',
                  subtitle:
                      'Short proof of the coaching style before you subscribe.',
                ),
                const SizedBox(height: 12),
                AppPanel(
                  color: AppTheme.surfaceColor,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Rating ${mentor.rating.toStringAsFixed(1)}/5',
                        style:
                            Theme.of(context).textTheme.titleMedium?.copyWith(
                                  color: accentColor,
                                ),
                      ),
                      const SizedBox(height: 10),
                      if (_isLoadingReviews)
                        const CircularProgressIndicator()
                      else if (_reviews.isEmpty)
                        const Text('Još nema recenzija.')
                      else
                        ..._reviews.map((review) => Padding(
                              padding: const EdgeInsets.only(bottom: 10),
                              child: Text(
                                  '${review['clientName'] ?? 'Klijent'} • ${review['rating']}/5\n${review['comment'] ?? ''}'),
                            )),
                      const SizedBox(height: 12),
                      OutlinedButton(
                          onPressed: _writeReview,
                          child: const Text('Napiši recenziju')),
                    ],
                  ),
                ),
                const SizedBox(height: 24),
                const SectionHeader(
                  title: 'Next step',
                  subtitle:
                      'The flow is mentor detail -> questionnaire -> subscription.',
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: OutlinedButton(
                        onPressed: () {
                          Navigator.of(context).push(
                            MaterialPageRoute<void>(
                              builder: (_) =>
                                  SubscriptionScreen(mentor: mentor),
                            ),
                          );
                        },
                        child: const Text('View subscription'),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: ElevatedButton(
                        onPressed: () {
                          Navigator.of(context).push(
                            MaterialPageRoute<void>(
                              builder: (_) =>
                                  QuestionnaireScreen(mentor: mentor),
                            ),
                          );
                        },
                        child: const Text('Start questionnaire'),
                      ),
                    ),
                  ],
                ),
              ]),
            ),
          ),
        ],
      ),
    );
  }
}

class _TagStat extends StatelessWidget {
  const _TagStat({
    required this.label,
    required this.accentColor,
  });

  final String label;
  final Color accentColor;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: accentColor.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
              color: accentColor,
            ),
      ),
    );
  }
}
