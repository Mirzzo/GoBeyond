enum AppRole { admin, mentor, client }

extension AppRoleParser on AppRole {
  static AppRole fromJsonValue(dynamic value) {
    if (value is int) {
      return switch (value) {
        1 => AppRole.admin,
        2 => AppRole.mentor,
        3 => AppRole.client,
        _ => AppRole.client,
      };
    }

    final normalized = value?.toString().toLowerCase() ?? '';
    return switch (normalized) {
      'admin' => AppRole.admin,
      'mentor' => AppRole.mentor,
      'client' => AppRole.client,
      _ => AppRole.client,
    };
  }
}
