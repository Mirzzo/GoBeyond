/// Mirrors the backend `UserRole` enum (api-contract.md section 0): the JSON
/// value is always the exact string `Admin` | `Mentor` | `Client`.
enum AppRole {
  admin('Admin', 'Administrator'),
  mentor('Mentor', 'Mentor'),
  client('Client', 'Klijent');

  const AppRole(this.wireValue, this.displayName);

  final String wireValue;
  final String displayName;

  static AppRole fromWire(dynamic value) {
    final normalized = value?.toString() ?? '';
    return AppRole.values.firstWhere(
      (role) => role.wireValue == normalized,
      orElse: () => AppRole.client,
    );
  }
}
