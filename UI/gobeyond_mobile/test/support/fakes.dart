// Fake repository implementations used across widget tests so no test ever
// touches the real network (there is no backend reachable in CI/this
// sandbox). Each fake implements the same abstract repository interface the
// real `Api*Repository` classes implement, so screens under test cannot tell
// the difference.
import 'dart:typed_data';

import 'package:gobeyond_mobile/data/models/lookup_item.dart';
import 'package:gobeyond_mobile/data/models/mentor_summary.dart';
import 'package:gobeyond_mobile/data/models/notification_item.dart';
import 'package:gobeyond_mobile/data/models/progress_entry.dart';
import 'package:gobeyond_mobile/data/models/questionnaire.dart';
import 'package:gobeyond_mobile/data/models/recommendation.dart';
import 'package:gobeyond_mobile/data/models/review.dart';
import 'package:gobeyond_mobile/data/models/subscription.dart';
import 'package:gobeyond_mobile/data/models/training_plan.dart';
import 'package:gobeyond_mobile/data/models/user_profile.dart';
import 'package:gobeyond_mobile/data/repositories/activity_repository.dart';
import 'package:gobeyond_mobile/data/repositories/auth_repository.dart';
import 'package:gobeyond_mobile/data/repositories/lookup_repository.dart';
import 'package:gobeyond_mobile/data/repositories/mentor_repository.dart';
import 'package:gobeyond_mobile/data/repositories/notification_repository.dart';
import 'package:gobeyond_mobile/data/repositories/profile_repository.dart';
import 'package:gobeyond_mobile/data/repositories/progress_repository.dart';
import 'package:gobeyond_mobile/core/network/api_exception.dart';
import 'package:gobeyond_mobile/data/repositories/subscription_repository.dart';

class FakeAuthRepository implements AuthRepository {
  Map<String, dynamic>? loginResponse;
  ApiException? loginError;

  @override
  Future<Map<String, dynamic>> login(
      String usernameOrEmail, String password) async {
    if (loginError != null) throw loginError!;
    return loginResponse ?? _authResponse();
  }

  @override
  Future<Map<String, dynamic>> registerClient(
      Map<String, dynamic> payload) async {
    return _authResponse();
  }

  @override
  Future<void> logout(String refreshToken) async {}

  @override
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmPassword,
  }) async {}

  Map<String, dynamic> _authResponse() => {
        'accessToken': 'token',
        'refreshToken': 'refresh',
        'expiresAt': DateTime.now().toIso8601String(),
        'user': {
          'id': 1,
          'username': 'client',
          'firstName': 'Test',
          'lastName': 'Client',
          'email': 'client@test.local',
          'role': 'Client',
          'profileImageUrl': null,
        },
      };
}

class FakeProfileRepository implements ProfileRepository {
  @override
  Future<void> deletePhoto() async {}

  @override
  Future<UserProfile> getMyProfile() async => UserProfile.fromJson({
        'username': 'client',
        'firstName': 'Test',
        'lastName': 'Client',
        'email': 'client@test.local',
        'phoneNumber': null,
        'dateOfBirth': '2000-01-01',
        'genderId': 1,
        'genderName': 'Muško',
        'role': 'Client',
        'profileImageUrl': null,
        'client': {
          'weightKg': 80,
          'heightCm': 180,
          'fitnessLevelId': 1,
          'fitnessLevelName': 'Početnik',
          'trainingExperienceYears': 1,
          'fitnessGoalId': 1,
          'fitnessGoalName': 'Snaga',
          'goalDescription': null,
          'preferredTrainingTypeId': null,
        },
      });

  @override
  Future<UserProfile> updateMyProfile(Map<String, dynamic> payload) =>
      getMyProfile();

  @override
  Future<String> uploadPhoto(Uint8List bytes, String fileName) async =>
      '/uploads/photo.png';
}

class FakeActivityRepository implements ActivityRepository {
  int heartbeatCalls = 0;

  @override
  Future<void> sendHeartbeat() async {
    heartbeatCalls++;
  }
}

class FakeLookupRepository implements LookupRepository {
  @override
  Future<List<LookupItem>> getFitnessGoals() async => const [
        LookupItem(id: 1, name: 'Mršavljenje'),
        LookupItem(id: 2, name: 'Snaga')
      ];

  @override
  Future<List<LookupItem>> getFitnessLevels() async => const [
        LookupItem(id: 1, name: 'Početnik'),
        LookupItem(id: 2, name: 'Napredni')
      ];

  @override
  Future<List<LookupItem>> getGenders() async => const [
        LookupItem(id: 1, name: 'Muško'),
        LookupItem(id: 2, name: 'Žensko')
      ];

  @override
  Future<List<LookupItem>> getTrainingTypes() async => const [
        LookupItem(id: 1, name: 'Weightlifting'),
        LookupItem(id: 2, name: 'Calisthenics'),
        LookupItem(id: 3, name: 'Hybrid'),
      ];
}

class FakeMentorRepository implements MentorRepository {
  final List<Map<String, dynamic>> calls = [];

  @override
  Future<List<MentorSummary>> getMentors({
    int? trainingTypeId,
    String? search,
    String sortBy = 'rating',
    String sortDirection = 'desc',
  }) async {
    calls.add({
      'trainingTypeId': trainingTypeId,
      'search': search,
      'sortBy': sortBy,
      'sortDirection': sortDirection,
    });
    return [
      MentorSummary.fromJson({
        'mentorProfileId': 1,
        'fullName': 'Marko Marković',
        'trainingTypeId': trainingTypeId ?? 1,
        'trainingTypeName': 'Weightlifting',
        'averageRating': 4.5,
        'reviewCount': 10,
        'monthlyPrice': 19.99,
        'currency': 'USD',
        'yearsOfExperience': 5,
        'age': 30,
      }),
    ];
  }

  @override
  Future<MentorDetail> getMentorById(int mentorProfileId) async =>
      MentorDetail.fromJson({
        'mentorProfileId': mentorProfileId,
        'fullName': 'Marko Marković',
        'trainingTypeId': 1,
        'trainingTypeName': 'Weightlifting',
        'averageRating': 4.5,
        'reviewCount': 10,
        'monthlyPrice': 19.99,
        'currency': 'USD',
        'yearsOfExperience': 5,
        'age': 30,
        'bio': 'Iskusan mentor.',
        'specializationNames': <String>[],
        'reviews': <Map<String, dynamic>>[],
      });

  @override
  Future<List<Review>> getMentorReviews(int mentorProfileId) async => const [];

  @override
  Future<List<MentorRecommendation>> getRecommendedMentors(
          {int take = 5}) async =>
      [];

  @override
  Future<List<MentorSummary>> getSimilarMentors(int mentorProfileId,
          {int take = 3}) async =>
      [];
}

class FakeProgressRepository implements ProgressRepository {
  final Map<String, ProgressEntryItem?> entriesByKey = {};
  final List<String> getEntryCalls = [];

  String _key(int year, int month) => '$year-$month';

  @override
  Future<ProgressEntryItem?> getEntry(int year, int month) async {
    getEntryCalls.add(_key(year, month));
    return entriesByKey[_key(year, month)];
  }

  @override
  Future<List<ProgressEntryItem>> getEntries(int year) async => const [];

  @override
  Future<List<int>> getYears() async => [2024, 2025];

  @override
  Future<List<WeightPoint>> getChart() async => const [];

  @override
  Future<TrainingPlan?> getPlanSnapshot(int year, int month) async => null;

  @override
  Future<ProgressEntryItem> upsertEntry({
    required int year,
    required int month,
    required num weightKg,
    required String measurements,
    required String strength,
    required String conditioning,
  }) async {
    throw UnimplementedError();
  }

  @override
  Future<ProgressEntryItem> uploadPhoto(
      int year, int month, Uint8List bytes, String fileName) async {
    throw UnimplementedError();
  }
}

class FakeSubscriptionRepository implements SubscriptionRepository {
  List<Subscription> subscriptions = [];

  @override
  Future<Subscription> cancelSubscription(int id) async => subscriptions.first;

  @override
  Future<Subscription> createSubscription({
    required int mentorProfileId,
    required Questionnaire questionnaire,
  }) async =>
      Subscription.fromJson({
        'id': 1,
        'mentorProfileId': mentorProfileId,
        'mentorFullName': 'Marko Marković',
        'trainingTypeName': 'Weightlifting',
        'status': 'PendingPayment',
        'price': 19.99,
        'currency': 'USD',
        'createdAt': DateTime.now().toIso8601String(),
        'canReview': false,
        'canRenew': false,
        'canCancel': true,
      });

  @override
  Future<Subscription> getSubscriptionDetail(int id) async =>
      subscriptions.first;

  @override
  Future<List<Subscription>> getMySubscriptions({String? status}) async =>
      subscriptions;
}

class FakeNotificationRepository implements NotificationRepository {
  @override
  Future<List<NotificationItem>> getNotifications(
          {bool unreadOnly = false, String? search}) async =>
      const [];

  @override
  Future<int> getUnreadCount() async => 0;

  @override
  Future<void> markAllRead() async {}

  @override
  Future<void> markRead(int id) async {}
}
