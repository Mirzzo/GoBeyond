class MentorModel {
  MentorModel({
    required this.id,
    required this.name,
    required this.category,
    required this.rating,
    required this.price,
    this.photoUrl,
  });

  final int id;
  final String name;
  final String category;
  final double rating;
  final double price;
  final String? photoUrl;
}
