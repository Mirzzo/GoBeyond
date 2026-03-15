import 'package:flutter/material.dart';

import '../../../core/network/dio_client.dart';
import '../../../core/theme/app_theme.dart';
import '../../../data/models/mentor_model.dart';
import '../../../data/repositories/mentor_repository.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/section_header.dart';
import 'mentor_detail_screen.dart';
import 'questionnaire_screen.dart';

class MentorListScreen extends StatefulWidget {
  const MentorListScreen({super.key});

  @override
  State<MentorListScreen> createState() => _MentorListScreenState();
}

class _MentorListScreenState extends State<MentorListScreen> {
  final MentorRepository _repository = MentorRepository(DioClient());
  final List<String> _filters = const ['All', 'Hybrid', 'Calisthenics', 'Weightlifting'];
  List<MentorModel> _mentors = const [];
  String _selectedFilter = 'All';
  String _searchQuery = '';
  bool _isLoading = true;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _loadMentors();
  }

  Future<void> _loadMentors() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final mentors = await _repository.getMentors(
        search: _searchQuery,
        category: _selectedFilter,
      );
      if (!mounted) {
        return;
      }

      setState(() {
        _mentors = mentors;
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

  void _openMentor(MentorModel mentor) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => MentorDetailScreen(mentor: mentor),
      ),
    );
  }

  void _openQuestionnaire(MentorModel mentor) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => QuestionnaireScreen(mentor: mentor),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(20, 18, 20, 0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Mentors', style: Theme.of(context).textTheme.displaySmall),
            const SizedBox(height: 6),
            Text(
              'Search and compare coaches before starting the questionnaire.',
              style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                    color: AppTheme.textMutedColor,
                  ),
            ),
            const SizedBox(height: 20),
            TextField(
              onChanged: (value) => _searchQuery = value,
              onSubmitted: (_) => _loadMentors(),
              decoration: InputDecoration(
                prefixIcon: const Icon(Icons.search_rounded),
                hintText: 'Search by mentor, category or coaching style',
                suffixIcon: IconButton(
                  onPressed: _loadMentors,
                  icon: const Icon(Icons.tune_rounded),
                ),
              ),
            ),
            const SizedBox(height: 14),
            SizedBox(
              height: 42,
              child: ListView.separated(
                scrollDirection: Axis.horizontal,
                itemCount: _filters.length,
                separatorBuilder: (_, __) => const SizedBox(width: 8),
                itemBuilder: (context, index) {
                  final filter = _filters[index];
                  return ChoiceChip(
                    label: Text(filter),
                    selected: _selectedFilter == filter,
                    onSelected: (_) {
                      setState(() => _selectedFilter = filter);
                      _loadMentors();
                    },
                  );
                },
              ),
            ),
            const SizedBox(height: 20),
            SectionHeader(
              title: '${_mentors.length} mentors available',
              subtitle: 'Every list view keeps a search parameter for faster filtering.',
            ),
            const SizedBox(height: 12),
            Expanded(
              child: _isLoading
                  ? const Center(child: CircularProgressIndicator())
                  : _errorMessage != null
                      ? Center(
                          child: Text(
                            _errorMessage!,
                            style: const TextStyle(color: Colors.redAccent),
                          ),
                        )
                      : ListView.separated(
                          itemCount: _mentors.length,
                          separatorBuilder: (_, __) => const SizedBox(height: 12),
                          itemBuilder: (context, index) {
                            final mentor = _mentors[index];
                            final accentColor = Color(mentor.accentColorValue);

                            return AppPanel(
                              onTap: () => _openMentor(mentor),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Row(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      CircleAvatar(
                                        radius: 28,
                                        backgroundColor: accentColor.withValues(alpha: 0.18),
                                        child: Text(
                                          mentor.name
                                              .split(' ')
                                              .where((part) => part.isNotEmpty)
                                              .map((part) => part[0])
                                              .take(2)
                                              .join(),
                                          style: TextStyle(
                                            color: accentColor,
                                            fontWeight: FontWeight.w700,
                                          ),
                                        ),
                                      ),
                                      const SizedBox(width: 14),
                                      Expanded(
                                        child: Column(
                                          crossAxisAlignment: CrossAxisAlignment.start,
                                          children: [
                                            Row(
                                              children: [
                                                Expanded(
                                                  child: Text(
                                                    mentor.name,
                                                    style: Theme.of(context).textTheme.titleLarge,
                                                  ),
                                                ),
                                                Text(
                                                  '\$${mentor.price.toStringAsFixed(0)}/mo',
                                                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                                                        color: accentColor,
                                                      ),
                                                ),
                                              ],
                                            ),
                                            const SizedBox(height: 6),
                                            Text(
                                              '${mentor.category} | ${mentor.city}',
                                              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                                                    color: AppTheme.textMutedColor,
                                                  ),
                                            ),
                                            const SizedBox(height: 10),
                                            Text(mentor.headline),
                                          ],
                                        ),
                                      ),
                                    ],
                                  ),
                                  const SizedBox(height: 14),
                                  Wrap(
                                    spacing: 8,
                                    runSpacing: 8,
                                    children: mentor.specialties
                                        .map((specialty) => Chip(label: Text(specialty)))
                                        .toList(),
                                  ),
                                  const SizedBox(height: 16),
                                  Row(
                                    children: [
                                      _MentorMiniStat(
                                        label: 'Rating',
                                        value: mentor.rating.toStringAsFixed(1),
                                        accentColor: accentColor,
                                      ),
                                      const SizedBox(width: 10),
                                      _MentorMiniStat(
                                        label: 'Clients',
                                        value: '${mentor.activeClients}',
                                        accentColor: accentColor,
                                      ),
                                      const SizedBox(width: 10),
                                      _MentorMiniStat(
                                        label: 'Response',
                                        value: mentor.responseTimeLabel,
                                        accentColor: accentColor,
                                      ),
                                    ],
                                  ),
                                  const SizedBox(height: 16),
                                  Row(
                                    children: [
                                      Expanded(
                                        child: OutlinedButton(
                                          onPressed: () => _openMentor(mentor),
                                          child: const Text('View profile'),
                                        ),
                                      ),
                                      const SizedBox(width: 12),
                                      Expanded(
                                        child: ElevatedButton(
                                          onPressed: () => _openQuestionnaire(mentor),
                                          child: const Text('Questionnaire'),
                                        ),
                                      ),
                                    ],
                                  ),
                                ],
                              ),
                            );
                          },
                        ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MentorMiniStat extends StatelessWidget {
  const _MentorMiniStat({
    required this.label,
    required this.value,
    required this.accentColor,
  });

  final String label;
  final String value;
  final Color accentColor;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: accentColor.withValues(alpha: 0.10),
          borderRadius: BorderRadius.circular(18),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              label,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: AppTheme.textMutedColor,
                  ),
            ),
            const SizedBox(height: 6),
            Text(
              value,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: accentColor,
                  ),
            ),
          ],
        ),
      ),
    );
  }
}
