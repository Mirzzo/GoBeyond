class SubscriptionModel {
  SubscriptionModel({
    required this.id,
    required this.mentorId,
    required this.status,
    required this.mentorName,
    required this.planName,
    required this.paymentStatus,
    required this.renewalLabel,
    required this.checkInDay,
    required this.progressLabel,
    required this.primaryGoal,
    required this.hasPublishedPlan,
  });

  final int id;
  final int mentorId;
  final String status;
  final String mentorName;
  final String planName;
  final String paymentStatus;
  final String renewalLabel;
  final String checkInDay;
  final String progressLabel;
  final String? primaryGoal;
  final bool hasPublishedPlan;

  factory SubscriptionModel.fromJson(Map<String, dynamic> json) {
    return SubscriptionModel(
      id: json['id'] as int? ?? 0,
      mentorId: json['mentorId'] as int? ?? 0,
      status: json['status'] as String? ?? 'Pending',
      mentorName: json['mentorName'] as String? ?? '',
      planName: json['planName'] as String? ?? 'Coaching subscription',
      paymentStatus: json['paymentStatus'] as String? ?? 'Pending',
      renewalLabel: json['renewalLabel'] as String? ?? '',
      checkInDay: json['checkInDay'] as String? ?? '',
      progressLabel: json['progressLabel'] as String? ?? '',
      primaryGoal: json['primaryGoal'] as String?,
      hasPublishedPlan: json['hasPublishedPlan'] as bool? ?? false,
    );
  }
}
