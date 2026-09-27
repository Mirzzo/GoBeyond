/// Shared client-side validation used across every form. All messages are
/// Bosnian (ijekavica) and state the expected format/limits explicitly, per
/// the course UI rules. Server-side `errors{field:[...]}` messages are
/// layered on top of these in each screen (see `ApiException.fieldError`).
class Validators {
  const Validators._();

  static final _emailRegex = RegExp(r'^[\w.+-]+@[\w-]+\.[a-zA-Z]{2,}$');
  static final _phoneRegex = RegExp(r'^\+?[0-9 ()-]{6,20}$');
  static final _usernameRegex = RegExp(r'^[a-zA-Z0-9._]{3,30}$');
  static final _passwordRegex = RegExp(r'^(?=.*[A-Za-z])(?=.*\d).{8,64}$');

  static String? required(String? value, {String label = 'Ovo polje'}) {
    if (value == null || value.trim().isEmpty) {
      return '$label je obavezno.';
    }
    return null;
  }

  static String? email(String? value) {
    if (value == null || value.trim().isEmpty) {
      return 'Email je obavezan.';
    }
    if (!_emailRegex.hasMatch(value.trim())) {
      return 'Unesite email u formatu ime@domena.com.';
    }
    return null;
  }

  /// Phone number is optional across the app; only validated when non-empty.
  static String? optionalPhone(String? value) {
    if (value == null || value.trim().isEmpty) return null;
    if (!_phoneRegex.hasMatch(value.trim())) {
      return 'Unesite validan broj telefona (npr. +387 61 123 456).';
    }
    return null;
  }

  static String? username(String? value) {
    if (value == null || value.trim().isEmpty) {
      return 'Korisničko ime je obavezno.';
    }
    if (!_usernameRegex.hasMatch(value.trim())) {
      return 'Korisničko ime mora imati 3–30 znakova (slova, brojevi, tačka, donja crta).';
    }
    return null;
  }

  static String? password(String? value) {
    if (value == null || value.isEmpty) {
      return 'Lozinka je obavezna.';
    }
    if (!_passwordRegex.hasMatch(value)) {
      return 'Lozinka mora imati 8–64 znaka, uključujući barem jedno slovo i jedan broj.';
    }
    return null;
  }

  static String? confirmPassword(String? value, String original) {
    if (value == null || value.isEmpty) {
      return 'Potvrda lozinke je obavezna.';
    }
    if (value != original) {
      return 'Lozinke se ne podudaraju.';
    }
    return null;
  }

  static String? textLength(
    String? value, {
    required int min,
    required int max,
    String label = 'Polje',
    bool optional = false,
  }) {
    final trimmed = value?.trim() ?? '';
    if (trimmed.isEmpty) {
      if (optional) return null;
      return '$label je obavezno ($min–$max znakova).';
    }
    if (trimmed.length < min || trimmed.length > max) {
      return '$label mora imati između $min i $max znakova.';
    }
    return null;
  }

  static String? numberRange(
    String? value, {
    required num min,
    required num max,
    String label = 'Vrijednost',
    bool isInt = false,
  }) {
    if (value == null || value.trim().isEmpty) {
      return '$label je obavezno.';
    }
    final parsed =
        isInt ? int.tryParse(value.trim()) : double.tryParse(value.trim());
    if (parsed == null) {
      return isInt ? '$label mora biti cijeli broj.' : '$label mora biti broj.';
    }
    if (parsed < min || parsed > max) {
      return '$label mora biti između $min i $max.';
    }
    return null;
  }

  static String? dropdownRequired(Object? value, {String label = 'Ovo polje'}) {
    if (value == null) {
      return '$label je obavezno.';
    }
    return null;
  }
}
