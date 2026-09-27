import 'questionnaire.dart';

/// Bosnian label for a `Subscription.status` value, used on every status
/// chip (Pretplata screen, subscription history).
String subscriptionStatusLabel(String status) {
  switch (status) {
    case 'PendingPayment':
      return 'Čeka plaćanje';
    case 'AwaitingMentor':
      return 'Čeka prihvatanje mentora';
    case 'Active':
      return 'Aktivna';
    case 'Rejected':
      return 'Odbijena';
    case 'Cancelled':
      return 'Otkazana';
    case 'Expired':
      return 'Istekla';
    default:
      return status;
  }
}

class PaymentItem {
  const PaymentItem({
    required this.amount,
    required this.currency,
    required this.purpose,
    required this.status,
    required this.createdAt,
    this.paidAt,
  });

  final num amount;
  final String currency;
  final String purpose;
  final String status;
  final String createdAt;
  final String? paidAt;

  factory PaymentItem.fromJson(Map<String, dynamic> json) => PaymentItem(
        amount: json['amount'] as num? ?? 0,
        currency: json['currency']?.toString() ?? 'USD',
        purpose: json['purpose']?.toString() ?? 'Initial',
        status: json['status']?.toString() ?? 'Pending',
        createdAt: json['createdAt']?.toString() ?? '',
        paidAt: json['paidAt']?.toString(),
      );
}

class Subscription {
  Subscription({
    required this.id,
    required this.mentorProfileId,
    required this.mentorFullName,
    required this.trainingTypeName,
    required this.status,
    required this.price,
    required this.currency,
    required this.createdAt,
    required this.canReview,
    required this.canRenew,
    required this.canCancel,
    this.mentorPhotoUrl,
    this.startDate,
    this.endDate,
    this.statusReason,
    this.reviewId,
    this.questionnaire,
    this.payments = const [],
  });

  final int id;
  final int mentorProfileId;
  final String mentorFullName;
  final String? mentorPhotoUrl;
  final String trainingTypeName;
  final String status;
  final num price;
  final String currency;
  final String createdAt;
  final String? startDate;
  final String? endDate;
  final String? statusReason;
  final bool canReview;
  final int? reviewId;
  final bool canRenew;
  final bool canCancel;
  final Questionnaire? questionnaire;
  final List<PaymentItem> payments;

  factory Subscription.fromJson(Map<String, dynamic> json) => Subscription(
        id: json['id'] as int? ?? 0,
        mentorProfileId: json['mentorProfileId'] as int? ?? 0,
        mentorFullName: json['mentorFullName']?.toString() ?? '',
        mentorPhotoUrl: json['mentorPhotoUrl']?.toString(),
        trainingTypeName: json['trainingTypeName']?.toString() ?? '',
        status: json['status']?.toString() ?? 'PendingPayment',
        price: json['price'] as num? ?? 0,
        currency: json['currency']?.toString() ?? 'USD',
        createdAt: json['createdAt']?.toString() ?? '',
        startDate: json['startDate']?.toString(),
        endDate: json['endDate']?.toString(),
        statusReason: json['statusReason']?.toString(),
        canReview: json['canReview'] as bool? ?? false,
        reviewId: json['reviewId'] as int?,
        canRenew: json['canRenew'] as bool? ?? false,
        canCancel: json['canCancel'] as bool? ?? false,
        questionnaire: json['questionnaire'] != null
            ? Questionnaire.fromJson(
                Map<String, dynamic>.from(json['questionnaire'] as Map))
            : null,
        payments: (json['payments'] as List<dynamic>? ?? const [])
            .whereType<Map<String, dynamic>>()
            .map(PaymentItem.fromJson)
            .toList(),
      );
}
