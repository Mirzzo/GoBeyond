/// Shared client-side validators. Every validator returns `null` when the
/// value is valid, or a Bosnian message describing the expected format when
/// it is not (course rule: validation messages must state the expected
/// format, not just "invalid").
class Validators {
  const Validators._();

  static final _emailRegex = RegExp(r'^[\w.+-]+@[\w-]+(\.[\w-]+)*\.[a-zA-Z]{2,}$');
  static final _phoneRegex = RegExp(r'^\+?[0-9 ]{6,20}$');
  static final _usernameRegex = RegExp(r'^[a-zA-Z0-9._]{3,30}$');

  static String? required(String? value, {String label = 'Ovo polje'}) {
    if (value == null || value.trim().isEmpty) {
      return '$label je obavezno.';
    }
    return null;
  }

  static String? email(String? value, {bool required = true}) {
    final trimmed = value?.trim() ?? '';
    if (trimmed.isEmpty) {
      return required ? 'Email je obavezan.' : null;
    }
    if (!_emailRegex.hasMatch(trimmed)) {
      return 'Unesite email u formatu ime@domena.com.';
    }
    return null;
  }

  static String? phone(String? value, {bool required = false}) {
    final trimmed = value?.trim() ?? '';
    if (trimmed.isEmpty) {
      return required ? 'Broj telefona je obavezan.' : null;
    }
    if (!_phoneRegex.hasMatch(trimmed)) {
      return 'Unesite ispravan broj telefona (samo cifre, razmaci i opcioni +).';
    }
    return null;
  }

  static String? username(String? value) {
    final trimmed = value?.trim() ?? '';
    if (trimmed.isEmpty) {
      return 'Korisničko ime je obavezno.';
    }
    if (!_usernameRegex.hasMatch(trimmed)) {
      return 'Korisničko ime mora imati 3-30 znakova (slova, brojevi, tačka ili donja crta).';
    }
    return null;
  }

  static String? password(String? value) {
    final trimmed = value ?? '';
    if (trimmed.isEmpty) {
      return 'Lozinka je obavezna.';
    }
    if (trimmed.length < 8 || trimmed.length > 64 || !RegExp(r'[A-Za-z]').hasMatch(trimmed) || !RegExp(r'[0-9]').hasMatch(trimmed)) {
      return 'Lozinka mora imati 8-64 znaka, uključujući barem jedno slovo i jedan broj.';
    }
    return null;
  }

  static String? confirmPassword(String? value, String original) {
    if (value == null || value.isEmpty) {
      return 'Potvrdite lozinku.';
    }
    if (value != original) {
      return 'Lozinke se ne podudaraju.';
    }
    return null;
  }

  static String? lengthRange(String? value, int min, int max, {required String label}) {
    final trimmed = value?.trim() ?? '';
    if (trimmed.isEmpty) {
      return '$label je obavezno.';
    }
    if (trimmed.length < min || trimmed.length > max) {
      return '$label mora imati između $min i $max znakova.';
    }
    return null;
  }

  static String? optionalLengthRange(String? value, int min, int max, {required String label}) {
    final trimmed = value?.trim() ?? '';
    if (trimmed.isEmpty) {
      return null;
    }
    if (trimmed.length < min || trimmed.length > max) {
      return '$label mora imati između $min i $max znakova.';
    }
    return null;
  }

  static String? numberRange(String? value, num min, num max, {required String label, bool isInt = false, int? maxDecimals}) {
    final trimmed = value?.trim() ?? '';
    if (trimmed.isEmpty) {
      return '$label je obavezno.';
    }
    final parsed = isInt ? int.tryParse(trimmed) : num.tryParse(trimmed);
    if (parsed == null) {
      return isInt ? '$label mora biti cijeli broj.' : '$label mora biti broj.';
    }
    if (parsed < min || parsed > max) {
      return '$label mora biti između $min i $max.';
    }
    if (!isInt && maxDecimals != null) {
      // Compare the parsed value against itself rounded to maxDecimals
      // places (same rule as the backend's MaxDecimalPlaces: decimal.Round(v,
      // maxDecimals) == v), instead of counting characters after a literal
      // '.' — that rejected valid input like '24.990' and let '24999e-3'
      // (24.999) through.
      final asDouble = parsed.toDouble();
      final rounded = double.parse(asDouble.toStringAsFixed(maxDecimals));
      if (rounded != asDouble) {
        return '$label može imati najviše $maxDecimals decimale.';
      }
    }
    return null;
  }

  static String? requiredSelection(Object? value, {String label = 'Ovo polje'}) {
    if (value == null) {
      return '$label je obavezno.';
    }
    return null;
  }
}
