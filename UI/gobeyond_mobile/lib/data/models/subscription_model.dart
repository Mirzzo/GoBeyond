class SubscriptionModel {
  SubscriptionModel({
    required this.id,
    required this.status,
    required this.mentorName,
    required this.planName,
    required this.paymentStatus,
    required this.renewalLabel,
    required this.checkInDay,
    required this.progressLabel,
  });

  final int id;
  final String status;
  final String mentorName;
  final String planName;
  final String paymentStatus;
  final String renewalLabel;
  final String checkInDay;
  final String progressLabel;
}
