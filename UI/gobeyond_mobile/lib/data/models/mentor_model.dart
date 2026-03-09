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
}
