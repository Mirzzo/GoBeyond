class Review {
  const Review({
    required this.id,
    required this.clientFullName,
    required this.rating,
    required this.comment,
    required this.createdAt,
    this.clientPhotoUrl,
    this.isMine = false,
  });

  final int id;
  final String clientFullName;
  final String? clientPhotoUrl;
  final int rating;
  final String comment;
  final String createdAt;
  final bool isMine;

  factory Review.fromJson(Map<String, dynamic> json) => Review(
        id: json['id'] as int? ?? 0,
        clientFullName: json['clientFullName']?.toString() ?? '',
        clientPhotoUrl: json['clientPhotoUrl']?.toString(),
        rating: json['rating'] as int? ?? 0,
        comment: json['comment']?.toString() ?? '',
        createdAt: json['createdAt']?.toString() ?? '',
        isMine: json['isMine'] as bool? ?? false,
      );
}
