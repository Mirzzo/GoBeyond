import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/mentor_summary.dart';
import '../../../data/repositories/mentor_repository.dart';
import '../../widgets/app_network_image.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/gb_scaffold.dart';
import '../../widgets/primary_button.dart';
import '../../widgets/star_rating.dart';
import '../../widgets/state_views.dart';
import 'mentor_detail_screen.dart';

enum _SortOption { rating, name, price }

extension on _SortOption {
  String get apiValue => switch (this) {
        _SortOption.rating => 'rating',
        _SortOption.name => 'name',
        _SortOption.price => 'price',
      };

  String get label => switch (this) {
        _SortOption.rating => 'Recenzije',
        _SortOption.name => 'Ime i prezime',
        _SortOption.price => 'Cijena',
      };
}

/// Mockup 08: mentors for one training type, with search + sort.
class MentorListScreen extends StatefulWidget {
  const MentorListScreen({
    super.key,
    required this.trainingTypeId,
    required this.trainingTypeName,
    this.mentorRepository,
  });

  final int trainingTypeId;
  final String trainingTypeName;
  final MentorRepository? mentorRepository;

  @override
  State<MentorListScreen> createState() => _MentorListScreenState();
}

class _MentorListScreenState extends State<MentorListScreen> {
  late final MentorRepository _repository =
      widget.mentorRepository ?? ApiMentorRepository();

  final _searchController = TextEditingController();
  Timer? _debounce;
  _SortOption _sortOption = _SortOption.rating;
  bool _ascending = false;

  late Future<List<MentorSummary>> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  Future<List<MentorSummary>> _load() {
    return _repository.getMentors(
      trainingTypeId: widget.trainingTypeId,
      search: _searchController.text,
      sortBy: _sortOption.apiValue,
      sortDirection: _ascending ? 'asc' : 'desc',
    );
  }

  void _reload() => setState(() {
        _future = _load();
      });

  void _onSearchChanged(String _) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 350), _reload);
  }

  @override
  Widget build(BuildContext context) {
    return GbScaffold(
      title: '${widget.trainingTypeName} mentori',
      body: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Container(
              padding: const EdgeInsets.symmetric(vertical: 14),
              decoration: BoxDecoration(
                color: AppTheme.accent,
                borderRadius: BorderRadius.circular(18),
              ),
              child: Text(
                '${widget.trainingTypeName.toUpperCase()} MENTORI',
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: AppTheme.onAccent,
                  fontWeight: FontWeight.w800,
                  fontSize: 16,
                ),
              ),
            ),
            const SizedBox(height: 14),
            TextField(
              controller: _searchController,
              onChanged: _onSearchChanged,
              decoration: const InputDecoration(
                hintText: 'PRETRAŽI MENTORE',
                suffixIcon: Icon(Icons.search_rounded),
              ),
            ),
            const SizedBox(height: 14),
            Row(
              children: [
                const Text('SORT BY:',
                    style:
                        TextStyle(fontWeight: FontWeight.w800, fontSize: 13)),
                const SizedBox(width: 10),
                Expanded(
                  child: DropdownButtonFormField<_SortOption>(
                    initialValue: _sortOption,
                    isExpanded: true,
                    dropdownColor: AppTheme.panel,
                    decoration: const InputDecoration(
                      contentPadding:
                          EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                    ),
                    items: _SortOption.values
                        .map((option) => DropdownMenuItem(
                            value: option, child: Text(option.label)))
                        .toList(),
                    onChanged: (value) {
                      if (value == null) return;
                      setState(() => _sortOption = value);
                      _reload();
                    },
                  ),
                ),
                IconButton(
                  tooltip: _ascending ? 'Rastuće' : 'Opadajuće',
                  onPressed: () {
                    setState(() => _ascending = !_ascending);
                    _reload();
                  },
                  icon: Icon(
                    _ascending
                        ? Icons.arrow_upward_rounded
                        : Icons.arrow_downward_rounded,
                    color: AppTheme.accent,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Expanded(
              child: FutureBuilder<List<MentorSummary>>(
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
                  final mentors = snapshot.data!;
                  if (mentors.isEmpty) {
                    return const EmptyStateView(
                      message: 'Nema mentora koji odgovaraju pretrazi.',
                      icon: Icons.search_off_rounded,
                    );
                  }
                  return ListView.separated(
                    itemCount: mentors.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 18),
                    itemBuilder: (context, index) =>
                        _MentorCard(mentor: mentors[index]),
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

class _MentorCard extends StatelessWidget {
  const _MentorCard({required this.mentor});

  final MentorSummary mentor;

  @override
  Widget build(BuildContext context) {
    return AppPanel(
      child: Column(
        children: [
          StarRating(rating: mentor.averageRating),
          const SizedBox(height: 12),
          AppNetworkImage(
            url: mentor.profileImageUrl,
            width: 220,
            height: 220,
            borderRadius: 20,
            yellowBorder: true,
          ),
          const SizedBox(height: 14),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 10),
            decoration: BoxDecoration(
              color: AppTheme.accent,
              borderRadius: BorderRadius.circular(20),
            ),
            child: Text(
              mentor.fullName.toUpperCase(),
              style: const TextStyle(
                color: AppTheme.onAccent,
                fontWeight: FontWeight.w800,
              ),
            ),
          ),
          const SizedBox(height: 6),
          Text(
            Formatters.price(mentor.monthlyPrice, mentor.currency),
            style: const TextStyle(
                color: AppTheme.textMuted, fontWeight: FontWeight.w600),
          ),
          const SizedBox(height: 12),
          SizedBox(
            width: 200,
            child: PrimaryButton(
              label: 'VIŠE INFO...',
              height: 46,
              onPressed: () => Navigator.of(context).push(
                MaterialPageRoute(
                  builder: (_) => MentorDetailScreen(
                      mentorProfileId: mentor.mentorProfileId),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
