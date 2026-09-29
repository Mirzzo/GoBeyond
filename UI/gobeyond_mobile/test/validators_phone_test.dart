import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/utils/validators.dart';

void main() {
  group('Validators.optionalPhone matches the backend pattern exactly', () {
    test('accepts the backend-documented format', () {
      expect(Validators.optionalPhone('+387 61 123 456'), isNull);
      expect(Validators.optionalPhone('+387 61 123 4567'), isNull);
    });

    test('rejects a number missing the country code (backend requires it)', () {
      expect(Validators.optionalPhone('061123456'),
          'Telefon mora biti u formatu +387 6X XXX XXX.');
    });

    test('rejects a landline (backend requires a mobile 6X prefix)', () {
      expect(Validators.optionalPhone('+387 33 123 456'),
          'Telefon mora biti u formatu +387 6X XXX XXX.');
    });

    test('empty value is fine (phone is optional)', () {
      expect(Validators.optionalPhone(''), isNull);
      expect(Validators.optionalPhone(null), isNull);
    });
  });
}
