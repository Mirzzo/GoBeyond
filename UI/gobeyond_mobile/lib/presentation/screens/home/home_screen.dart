import 'package:flutter/material.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../data/models/lookup_item.dart';
import '../../../data/models/recommendation.dart';
import '../../../data/repositories/lookup_repository.dart';
import '../../../data/repositories/mentor_repository.dart';
import '../../widgets/app_network_image.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/gb_scaffold.dart';
import '../../widgets/gobeyond_logo.dart';
import '../../widgets/primary_button.dart';
import '../../widgets/section_header.dart';
import '../../widgets/star_rating.dart';
import '../mentor/mentor_detail_screen.dart';
import '../mentor/mentor_list_screen.dart';

IconData trainingTypeIcon(String name) {
  final normalized = name.toLowerCase();
  if (normalized.contains('weight') || normalized.contains('teg')) {
    return Icons.fitness_center_rounded;
  }
  if (normalized.contains('calisthenic')) {
    return Icons.accessibility_new_rounded;
  }
  if (normalized.contains('hybrid')) {
    return Icons.bolt_rounded;
  }
  return Icons.sports_gymnastics_rounded;
}

/// Mockup 07: the GOBEYOND panel with one big yellow button per training
/// type, and the "PREPOROUČENO ZA VAS" recommendation feed underneath.
class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key, this.lookupRepository, this.mentorRepository});

  final LookupRepository? lookupRepository;
  final MentorRepository? mentorRepository;

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  late final LookupRepository _lookupRepository =
      widget.lookupRepository ?? ApiLookupRepository();
  late final MentorRepository _mentorRepository =
      widget.mentorRepository ?? ApiMentorRepository();

  late Future<_HomeData> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<_HomeData> _load() async {
    final results = await Future.wait([
      _lookupRepository.getTrainingTypes(),
      _mentorRepository.getRecommendedMentors(take: 5),
    ]);
    return _HomeData(
      trainingTypes: results[0] as List<LookupItem>,
      recommendations: results[1] as List<MentorRecommendation>,
    );
  }

  @override
  Widget build(BuildContext context) {
    return GbScaffold(
      body: FutureBuilder<_HomeData>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            final message = ApiException.from(snapshot.error!).message;
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.error_outline_rounded,
                        color: AppTheme.danger, size: 40),
                    const SizedBox(height: 12),
                    Text(message, textAlign: TextAlign.center),
                    const SizedBox(height: 16),
                    SizedBox(
                      width: 180,
                      child: PrimaryButton(
                        label: 'Pokušaj ponovo',
                        onPressed: () => setState(() {
                          _future = _load();
                        }),
                      ),
                    ),
                  ],
                ),
              ),
            );
          }

          final data = snapshot.data!;
          return RefreshIndicator(
            onRefresh: () async {
              final next = _load();
              setState(() {
                _future = next;
              });
              await next;
            },
            child: ListView(
              padding: const EdgeInsets.all(20),
              children: [
                AppPanel(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      const Center(child: GoBeyondLogo()),
                      const SizedBox(height: 20),
                      if (data.trainingTypes.isEmpty)
                        const Padding(
                          padding: EdgeInsets.symmetric(vertical: 12),
                          child: Text(
                            'Trenutno nema dostupnih vrsta treninga.',
                            style: TextStyle(color: AppTheme.textMuted),
                            textAlign: TextAlign.center,
                          ),
                        )
                      else
                        for (final type in data.trainingTypes) ...[
                          PrimaryButton(
                            label: type.name.toUpperCase(),
                            icon: Icon(trainingTypeIcon(type.name),
                                color: AppTheme.onAccent),
                            onPressed: () => Navigator.of(context).push(
                              MaterialPageRoute(
                                builder: (_) => MentorListScreen(
                                  trainingTypeId: type.id,
                                  trainingTypeName: type.name,
                                ),
                              ),
                            ),
                          ),
                          const SizedBox(height: 16),
                        ],
                    ],
                  ),
                ),
                const SizedBox(height: 28),
                const SectionHeader(
                  title: 'PREPORUČENO ZA VAS',
                  subtitle: 'Mentori odabrani prema vašem profilu i ciljevima',
                ),
                const SizedBox(height: 14),
                if (data.recommendations.isEmpty)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 20),
                    child: Text(
                      'Popunite svoj profil kako bismo vam mogli preporučiti mentore.',
                      style: TextStyle(color: AppTheme.textMuted),
                    ),
                  )
                else
                  for (final recommendation in data.recommendations)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 14),
                      child:
                          _RecommendationCard(recommendation: recommendation),
                    ),
              ],
            ),
          );
        },
      ),
    );
  }
}

class _HomeData {
  const _HomeData({required this.trainingTypes, required this.recommendations});

  final List<LookupItem> trainingTypes;
  final List<MentorRecommendation> recommendations;
}

class _RecommendationCard extends StatelessWidget {
  const _RecommendationCard({required this.recommendation});

  final MentorRecommendation recommendation;

  @override
  Widget build(BuildContext context) {
    final mentor = recommendation.mentor;
    return AppPanel(
      onTap: () => Navigator.of(context).push(
        MaterialPageRoute(
          builder: (_) =>
              MentorDetailScreen(mentorProfileId: mentor.mentorProfileId),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          AppNetworkImage(
            url: mentor.profileImageUrl,
            width: 56,
            height: 56,
            borderRadius: 28,
            yellowBorder: true,
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(mentor.fullName,
                    style: const TextStyle(
                        fontWeight: FontWeight.w800, fontSize: 16)),
                const SizedBox(height: 4),
                StarRating(rating: mentor.averageRating, size: 16),
                const SizedBox(height: 6),
                for (final reason in recommendation.reasons.take(2))
                  Padding(
                    padding: const EdgeInsets.only(top: 2),
                    child: Text(
                      '• $reason',
                      style: const TextStyle(
                          color: AppTheme.textMuted, fontSize: 12.5),
                    ),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
