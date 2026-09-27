/// A row from one of the reference tables (`training-types`, `fitness-goals`,
/// `fitness-levels`, `genders`) used to fill every dropdown in the app.
class LookupItem {
  const LookupItem({required this.id, required this.name, this.description});

  final int id;
  final String name;
  final String? description;

  factory LookupItem.fromJson(Map<String, dynamic> json) => LookupItem(
        id: json['id'] as int,
        name: json['name']?.toString() ?? '',
        description: json['description']?.toString(),
      );

  @override
  bool operator ==(Object other) => other is LookupItem && other.id == id;

  @override
  int get hashCode => id.hashCode;
}
