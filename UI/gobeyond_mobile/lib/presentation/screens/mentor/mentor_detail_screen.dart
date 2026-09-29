import 'package:flutter/material.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/mentor_summary.dart';
import '../../../data/models/review.dart';
import '../../../data/models/subscription.dart';
import '../../../data/repositories/mentor_repository.dart';
import '../../../data/repositories/subscription_repository.dart';
import '../../widgets/app_network_image.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/app_modal_page.dart';
import '../../widgets/primary_button.dart';
import '../../widgets/star_rating.dart';
import '../../widgets/state_views.dart';
import 'questionnaire_screen.dart';

const _blockingStatuses = {'PendingPayment', 'AwaitingMentor', 'Active'};

/// Mockup 10: full-screen mentor detail modal with the KUPI PLAN call to
/// action.
class MentorDetailScreen extends StatefulWidget {
  const MentorDetailScreen({
    super.key,
    required this.mentorProfileId,
    this.mentorRepository,
    this.subscriptionRepository,
  });

  final int mentorProfileId;
  final MentorRepository? mentorRepository;
  final SubscriptionRepository? subscriptionRepository;

  @override
  State<MentorDetailScreen> createState() => _MentorDetailScreenState();
}

class _MentorDetailScreenState extends State<MentorDetailScreen> {
  late final MentorRepository _mentorRepository =
      widget.mentorRepository ?? ApiMentorRepository();
  late final SubscriptionRepository _subscriptionRepository =
      widget.subscriptionRepository ?? ApiSubscriptionRepository();

  late Future<_MentorDetailData> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<_MentorDetailData> _load() async {
    final results = await Future.wait([
      _mentorRepository.getMentorById(widget.mentorProfileId),
      _mentorRepository.getSimilarMentors(widget.mentorProfileId),
      _subscriptionRepository.getMySubscriptions(),
    ]);
    final subscriptions = results[2] as List<Subscription>;
    final blocking = subscriptions
        .where((s) => _blockingStatuses.contains(s.status))
        .toList();
    return _MentorDetailData(
      mentor: results[0] as MentorDetail,
      similar: results[1] as List<MentorSummary>,
      blockingStatus: blocking.isNotEmpty ? blocking.first.status : null,
    );
  }

  @override
  Widget build(BuildContext context) {
    return AppModalPage(
      body: FutureBuilder<_MentorDetailData>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const LoadingView();
          }
          if (snapshot.hasError) {
            return ErrorView(
              message: ApiException.from(snapshot.error!).message,
              onRetry: () => setState(() {
                _future = _load();
              }),
            );
          }
          final data = snapshot.data!;
          final mentor = data.mentor;

          return ListView(
            children: [
              ClipRRect(
                borderRadius: BorderRadius.circular(24),
                child: AppNetworkImage(
                  url: mentor.profileImageUrl,
                  height: 260,
                  borderRadius: 24,
                  placeholderIcon: Icons.person_rounded,
                ),
              ),
              const SizedBox(height: 18),
              Center(
                child: Column(
                  children: [
                    Text('IME: ${mentor.fullName.toUpperCase()}',
                        textAlign: TextAlign.center,
                        style: const TextStyle(
                            color: AppTheme.accent,
                            fontWeight: FontWeight.w800,
                            fontSize: 18)),
                    if (mentor.nickname != null && mentor.nickname!.isNotEmpty)
                      Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: Text('AKA ${mentor.nickname}',
                            style: const TextStyle(
                                color: AppTheme.accent,
                                fontWeight: FontWeight.w800,
                                fontSize: 16)),
                      ),
                    Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Text('GODINE: ${mentor.age}',
                          style: const TextStyle(
                              color: AppTheme.accent,
                              fontWeight: FontWeight.w800,
                              fontSize: 16)),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 10),
              Center(child: StarRating(rating: mentor.averageRating)),
              const SizedBox(height: 4),
              Center(
                child: Text(
                    '${mentor.reviewCount} ${Formatters.plural(mentor.reviewCount, 'recenzija', 'recenzije', 'recenzija')} · '
                    '${mentor.yearsOfExperience} god. iskustva',
                    style: const TextStyle(
                        color: AppTheme.textMuted, fontSize: 12.5)),
              ),
              const SizedBox(height: 20),
              const Text('OPIS',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                      color: AppTheme.accent,
                      fontWeight: FontWeight.w800,
                      fontSize: 18)),
              const SizedBox(height: 10),
              AppPanel(
                child: Text(
                  mentor.bio,
                  style: const TextStyle(height: 1.5),
                ),
              ),
              if (mentor.specializationNames.isNotEmpty) ...[
                const SizedBox(height: 16),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: mentor.specializationNames
                      .map((s) => Chip(label: Text(s)))
                      .toList(),
                ),
              ],
              const SizedBox(height: 22),
              Text('RECENZIJE (${mentor.reviews.length})',
                  style: const TextStyle(
                      fontWeight: FontWeight.w800, fontSize: 15)),
              const SizedBox(height: 10),
              if (mentor.reviews.isEmpty)
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 8),
                  child: Text('Još nema recenzija za ovog mentora.',
                      style: TextStyle(color: AppTheme.textMuted)),
                )
              else
                for (final review in mentor.reviews)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: _ReviewTile(review: review),
                  ),
              if (data.similar.isNotEmpty) ...[
                const SizedBox(height: 10),
                const Text('SLIČNI MENTORI',
                    style:
                        TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
                const SizedBox(height: 12),
                SizedBox(
                  height: 150,
                  child: ListView.separated(
                    scrollDirection: Axis.horizontal,
                    itemCount: data.similar.length,
                    separatorBuilder: (_, __) => const SizedBox(width: 12),
                    itemBuilder: (context, index) {
                      final similar = data.similar[index];
                      return _SimilarMentorCard(
                        mentor: similar,
                        currentMentorProfileId: widget.mentorProfileId,
                      );
                    },
                  ),
                ),
              ],
              const SizedBox(height: 24),
              if (data.blockingStatus != null)
                Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: Text(
                    _blockingMessage(data.blockingStatus!),
                    textAlign: TextAlign.center,
                    style: const TextStyle(
                        color: AppTheme.textMuted, fontSize: 12.5),
                  ),
                ),
              PrimaryButton(
                label:
                    'KUPI PLAN ${Formatters.price(mentor.monthlyPrice, mentor.currency)}',
                onPressed: data.blockingStatus != null
                    ? null
                    : () => Navigator.of(context).push(
                          MaterialPageRoute(
                            builder: (_) => QuestionnaireScreen(mentor: mentor),
                          ),
                        ),
              ),
              const SizedBox(height: 12),
            ],
          );
        },
      ),
    );
  }
}

/// An AwaitingMentor subscription is already paid (the Initial payment moves
/// it out of PendingPayment) and the client cannot cancel it while the
/// mentor decides, so its text says to wait for the mentor's answer. A
/// PendingPayment (not paid yet) or Active subscription can be cancelled by
/// the client, so for those the text says to cancel it first.
String _blockingMessage(String status) {
  if (status == 'AwaitingMentor') {
    return 'Vaš zahtjev kod mentora još čeka odgovor. Novog mentora možete '
        'odabrati kada mentor odgovori na zahtjev.';
  }
  return 'Već imate aktivnu ili započetu saradnju sa mentorom. '
      'Otkažite postojeću pretplatu prije nego odaberete novog mentora.';
}

class _MentorDetailData {
  const _MentorDetailData({
    required this.mentor,
    required this.similar,
    required this.blockingStatus,
  });

  final MentorDetail mentor;
  final List<MentorSummary> similar;

  /// The status of the client's blocking subscription with another mentor
  /// (PendingPayment/AwaitingMentor/Active), or null if there is none.
  final String? blockingStatus;
}

class _ReviewTile extends StatelessWidget {
  const _ReviewTile({required this.review});

  final Review review;

  @override
  Widget build(BuildContext context) {
    return AppPanel(
      color: AppTheme.surface,
      padding: const EdgeInsets.all(14),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          AppNetworkImage(
            url: review.clientPhotoUrl,
            width: 40,
            height: 40,
            borderRadius: 20,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(review.clientFullName,
                          style: const TextStyle(fontWeight: FontWeight.w700)),
                    ),
                    StarRating(rating: review.rating.toDouble(), size: 14),
                  ],
                ),
                const SizedBox(height: 4),
                Text(review.comment, style: const TextStyle(fontSize: 13.5)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SimilarMentorCard extends StatelessWidget {
  const _SimilarMentorCard(
      {required this.mentor, required this.currentMentorProfileId});

  final MentorSummary mentor;
  final int currentMentorProfileId;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 120,
      child: AppPanel(
        padding: const EdgeInsets.all(10),
        onTap: () {
          // Push (not pushReplacement) so Back returns to this mentor's
          // detail screen instead of skipping past it to the list; skip the
          // navigation entirely if the card points back at this same mentor.
          if (mentor.mentorProfileId == currentMentorProfileId) return;
          Navigator.of(context).push(
            MaterialPageRoute(
              builder: (_) =>
                  MentorDetailScreen(mentorProfileId: mentor.mentorProfileId),
            ),
          );
        },
        child: Column(
          children: [
            AppNetworkImage(
              url: mentor.profileImageUrl,
              width: 70,
              height: 70,
              borderRadius: 35,
              yellowBorder: true,
            ),
            const SizedBox(height: 8),
            Text(
              mentor.fullName,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style:
                  const TextStyle(fontWeight: FontWeight.w700, fontSize: 12.5),
            ),
            StarRating(rating: mentor.averageRating, size: 12),
          ],
        ),
      ),
    );
  }
}
