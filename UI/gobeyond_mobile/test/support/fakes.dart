// Fake repository implementations used across widget tests so no test ever
// touches the real network (there is no backend reachable in CI/this
// sandbox). Each fake implements the same abstract repository interface the
// real `Api*Repository` classes implement, so screens under test cannot tell
// the difference.
import 'dart:async';
import 'dart:typed_data';

import 'package:gobeyond_mobile/data/models/lookup_item.dart';
import 'package:gobeyond_mobile/data/models/mentor_summary.dart';
import 'package:gobeyond_mobile/data/models/message_thread.dart';
import 'package:gobeyond_mobile/data/models/notification_item.dart';
import 'package:gobeyond_mobile/data/models/progress_entry.dart';
import 'package:gobeyond_mobile/data/models/questionnaire.dart';
import 'package:gobeyond_mobile/data/models/recommendation.dart';
import 'package:gobeyond_mobile/data/models/review.dart';
import 'package:gobeyond_mobile/data/models/subscription.dart';
import 'package:gobeyond_mobile/data/models/training_plan.dart';
import 'package:gobeyond_mobile/data/models/training_session.dart';
import 'package:gobeyond_mobile/data/models/user_profile.dart';
import 'package:gobeyond_mobile/data/repositories/activity_repository.dart';
import 'package:gobeyond_mobile/data/repositories/auth_repository.dart';
import 'package:gobeyond_mobile/data/repositories/lookup_repository.dart';
import 'package:gobeyond_mobile/data/repositories/mentor_repository.dart';
import 'package:gobeyond_mobile/data/repositories/message_repository.dart';
import 'package:gobeyond_mobile/data/repositories/notification_repository.dart';
import 'package:gobeyond_mobile/data/repositories/profile_repository.dart';
import 'package:gobeyond_mobile/data/repositories/progress_repository.dart';
import 'package:gobeyond_mobile/core/network/api_exception.dart';
import 'package:gobeyond_mobile/data/repositories/subscription_repository.dart';
import 'package:gobeyond_mobile/data/repositories/training_plan_repository.dart';

class FakeAuthRepository implements AuthRepository {
  Map<String, dynamic>? loginResponse;
  ApiException? loginError;

  /// When set, registerClient fails with it (e.g. server validation errors).
  ApiException? registerError;

  @override
  Future<Map<String, dynamic>> login(
      String usernameOrEmail, String password) async {
    if (loginError != null) throw loginError!;
    return loginResponse ?? _authResponse();
  }

  @override
  Future<Map<String, dynamic>> registerClient(
      Map<String, dynamic> payload) async {
    if (registerError != null) throw registerError!;
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
  String dateOfBirth = '2000-01-01';

  /// When set, updateMyProfile fails with it (e.g. server validation errors).
  ApiException? updateError;

  @override
  Future<void> deletePhoto() async {}

  @override
  Future<UserProfile> getMyProfile() async => UserProfile.fromJson({
        'username': 'client',
        'firstName': 'Test',
        'lastName': 'Client',
        'email': 'client@test.local',
        'phoneNumber': null,
        'dateOfBirth': dateOfBirth,
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
  Future<UserProfile> updateMyProfile(Map<String, dynamic> payload) async {
    if (updateError != null) throw updateError!;
    return getMyProfile();
  }

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

  /// Keyed by mentorProfileId so a test can serve a distinct detail per id
  /// (e.g. for a detail -> similar -> detail -> Back navigation chain).
  Map<int, MentorDetail>? mentorsById;
  List<MentorSummary> similarMentors = const [];
  List<MentorRecommendation> recommendedMentors = const [];

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
  Future<MentorDetail> getMentorById(int mentorProfileId) async {
    final byId = mentorsById;
    if (byId != null && byId.containsKey(mentorProfileId)) {
      return byId[mentorProfileId]!;
    }
    return MentorDetail.fromJson({
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
  }

  @override
  Future<List<Review>> getMentorReviews(int mentorProfileId) async => const [];

  @override
  Future<List<MentorRecommendation>> getRecommendedMentors(
          {int take = 5}) async =>
      recommendedMentors;

  @override
  Future<List<MentorSummary>> getSimilarMentors(int mentorProfileId,
          {int take = 3}) async =>
      similarMentors;
}

class FakeProgressRepository implements ProgressRepository {
  final Map<String, ProgressEntryItem?> entriesByKey = {};
  final List<String> getEntryCalls = [];
  List<WeightPoint> chart = const [];
  int getChartCalls = 0;
  int getYearsCalls = 0;

  /// While set, getYears/getEntry/getChart wait for it to complete, so a
  /// test can look at the screen while a reload is still in flight.
  Completer<void>? gate;

  String _key(int year, int month) => '$year-$month';

  @override
  Future<ProgressEntryItem?> getEntry(int year, int month) async {
    getEntryCalls.add(_key(year, month));
    await gate?.future;
    return entriesByKey[_key(year, month)];
  }

  @override
  Future<List<ProgressEntryItem>> getEntries(int year) async => const [];

  @override
  Future<List<int>> getYears() async {
    getYearsCalls++;
    await gate?.future;
    return [2024, 2025];
  }

  @override
  Future<List<WeightPoint>> getChart() async {
    getChartCalls++;
    await gate?.future;
    return chart;
  }

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
    final entry = ProgressEntryItem(
      id: 1,
      year: year,
      month: month,
      monthName: '$month',
      weightKg: weightKg,
      measurements: measurements,
      strength: strength,
      conditioning: conditioning,
      hasPlanSnapshot: false,
    );
    entriesByKey[_key(year, month)] = entry;
    return entry;
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

  int getMySubscriptionsCalls = 0;

  /// While set, getMySubscriptions waits for it to complete.
  Completer<void>? gate;

  @override
  Future<List<Subscription>> getMySubscriptions({String? status}) async {
    getMySubscriptionsCalls++;
    await gate?.future;
    return subscriptions;
  }
}

class FakeTrainingPlanRepository implements TrainingPlanRepository {
  TrainingPlan? plan;
  List<TrainingSessionItem> sessions = const [];
  int getMyCurrentPlanCalls = 0;

  /// While set, getMyCurrentPlan waits for it to complete.
  Completer<void>? gate;

  @override
  Future<TrainingPlan> getMyCurrentPlan() async {
    getMyCurrentPlanCalls++;
    await gate?.future;
    final current = plan;
    if (current == null) {
      throw ApiException('Nema aktivnog plana.', statusCode: 404);
    }
    return current;
  }

  @override
  Future<TrainingSessionItem> completeSession({
    required int planId,
    required int dayOfWeek,
    required int repetitions,
    String? note,
  }) async {
    throw UnimplementedError();
  }

  @override
  Future<List<TrainingSessionItem>> getSessions(int planId) async => sessions;
}

class FakeMessageRepository implements MessageRepository {
  List<MessageItem> messages = [];
  int getThreadMessagesCalls = 0;

  // When true, getThreadMessages() doesn't resolve on its own: each call
  // is queued as a Completer the test resolves (or fails) individually, in
  // any order, to reproduce overlapping loads and polls.
  bool manualResponses = false;
  final List<Completer<List<MessageItem>>> pendingResponses = [];

  @override
  Future<List<MessageThread>> getThreads({String? search}) async => [];

  @override
  Future<List<MessageItem>> getThreadMessages(int subscriptionId) async {
    getThreadMessagesCalls++;
    if (manualResponses) {
      final completer = Completer<List<MessageItem>>();
      pendingResponses.add(completer);
      return completer.future;
    }
    return messages;
  }

  /// Resolves the [index]-th call made while [manualResponses] was on.
  void resolveResponse(int index, List<MessageItem> result) {
    pendingResponses[index].complete(result);
  }

  /// Fails the [index]-th call made while [manualResponses] was on.
  void failResponse(int index, Object error) {
    pendingResponses[index].completeError(error);
  }

  @override
  Future<MessageItem> sendMessage(int subscriptionId, String content) async {
    final message = MessageItem(
      id: messages.length + 1,
      content: content,
      sentAt: DateTime.now().toIso8601String(),
      isMine: true,
      senderName: 'Test Client',
    );
    messages = [...messages, message];
    return message;
  }
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
