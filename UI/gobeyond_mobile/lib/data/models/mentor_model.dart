class MentorModel {
  MentorModel({
    required this.id,
    required this.name,
    required this.category,
    required this.rating,
    required this.price,
    required this.headline,
    required this.city,
    required this.about,
    required this.specialties,
    required this.nextStartLabel,
    required this.responseTimeLabel,
    required this.reviewQuote,
    required this.activeClients,
    required this.accentColorValue,
    this.photoUrl,
  });

  final int id;
  final String name;
  final String category;
  final double rating;
  final double price;
  final String headline;
  final String city;
  final String about;
  final List<String> specialties;
  final String nextStartLabel;
  final String responseTimeLabel;
  final String reviewQuote;
  final int activeClients;
  final int accentColorValue;
  final String? photoUrl;

  factory MentorModel.fromJson(Map<String, dynamic> json) {
    return MentorModel(
      id: json['id'] as int? ?? 0,
      name: json['fullName'] as String? ?? '',
      category: json['category'] as String? ?? 'Hybrid',
      rating: (json['averageRating'] as num?)?.toDouble() ?? 0,
      price: (json['price'] as num?)?.toDouble() ?? 0,
      headline: json['headline'] as String? ?? '',
      city: json['city'] as String? ?? 'Remote',
      about: json['bio'] as String? ?? '',
      specialties: (json['specialties'] as List<dynamic>? ?? const [])
          .map((item) => item.toString())
          .toList(),
      nextStartLabel: json['nextStartLabel'] as String? ?? 'Starts soon',
      responseTimeLabel: json['responseTimeLabel'] as String? ?? 'Same-day feedback',
      reviewQuote: json['reviewQuote'] as String? ?? 'Structured coaching with weekly feedback.',
      activeClients: json['activeClients'] as int? ?? 0,
      accentColorValue: json['accentColorValue'] as int? ?? 0xFFF2A541,
      photoUrl: json['profileImageUrl'] as String?,
    );
  }
}
