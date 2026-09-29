import 'package:flutter/material.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../data/models/recommendation.dart';
import '../../../data/repositories/mentor_repository.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/gb_scaffold.dart';
import '../../widgets/app_network_image.dart';
import '../../widgets/primary_button.dart';
import '../../widgets/star_rating.dart';
import '../../widgets/state_views.dart';
import 'mentor_detail_screen.dart';

/// "Preporučeni mentori" (Ostalo..): the full content-based recommendation
/// list, with the scored reasons shown per mentor.
class RecommendedMentorsScreen extends StatefulWidget {
  const RecommendedMentorsScreen({super.key, this.mentorRepository});

  final MentorRepository? mentorRepository;

  @override
  State<RecommendedMentorsScreen> createState() =>
      _RecommendedMentorsScreenState();
}

class _RecommendedMentorsScreenState extends State<RecommendedMentorsScreen> {
  late final MentorRepository _repository =
      widget.mentorRepository ?? ApiMentorRepository();

  late Future<List<MentorRecommendation>> _future;

  @override
  void initState() {
    super.initState();
    _future = _repository.getRecommendedMentors(take: 20);
  }

  void _reload() => setState(() {
        _future = _repository.getRecommendedMentors(take: 20);
      });

  @override
  Widget build(BuildContext context) {
    return GbScaffold(
      title: 'Preporučeni mentori',
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Padding(
            padding: EdgeInsets.fromLTRB(20, 16, 20, 0),
            child: Text('PREPORUČENI MENTORI',
                style: TextStyle(fontWeight: FontWeight.w800, fontSize: 18)),
          ),
          Expanded(
            child: FutureBuilder<List<MentorRecommendation>>(
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
                final recommendations = snapshot.data!;
                if (recommendations.isEmpty) {
                  return const EmptyStateView(
                    message:
                        'Popunite svoj profil kako bismo vam mogli preporučiti mentore.',
                    icon: Icons.recommend_rounded,
                  );
                }
                return ListView.separated(
                  padding: const EdgeInsets.all(20),
                  itemCount: recommendations.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 14),
                  itemBuilder: (context, index) {
                    final recommendation = recommendations[index];
                    final mentor = recommendation.mentor;
                    return AppPanel(
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          AppNetworkImage(
                            url: mentor.profileImageUrl,
                            width: 64,
                            height: 64,
                            borderRadius: 32,
                            yellowBorder: true,
                          ),
                          const SizedBox(width: 14),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(mentor.fullName,
                                    style: const TextStyle(
                                        fontWeight: FontWeight.w800,
                                        fontSize: 15)),
                                const SizedBox(height: 4),
                                StarRating(
                                    rating: mentor.averageRating, size: 15),
                                const SizedBox(height: 6),
                                for (final reason in recommendation.reasons)
                                  Text('• $reason',
                                      style: const TextStyle(
                                          color: AppTheme.textMuted,
                                          fontSize: 12)),
                                const SizedBox(height: 10),
                                SizedBox(
                                  width: 150,
                                  child: PrimaryButton(
                                    label: 'VIŠE INFO...',
                                    height: 46,
                                    onPressed: () => Navigator.of(context).push(
                                      MaterialPageRoute(
                                        builder: (_) => MentorDetailScreen(
                                            mentorProfileId:
                                                mentor.mentorProfileId),
                                      ),
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                    );
                  },
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
