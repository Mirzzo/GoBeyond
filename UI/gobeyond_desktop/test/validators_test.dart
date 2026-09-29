import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_desktop/core/utils/validators.dart';

void main() {
  group('Validators.email', () {
    test('rejects empty value with a required message', () {
      expect(Validators.email(''), 'Email je obavezan.');
    });

    test('rejects malformed addresses with a format hint', () {
      expect(Validators.email('not-an-email'), 'Unesite email u formatu ime@domena.com.');
    });

    test('accepts a valid address', () {
      expect(Validators.email('ime@domena.com'), isNull);
    });

    test('accepts multi-label domains', () {
      expect(Validators.email('elmir.babovic@edu.fit.ba'), isNull);
      expect(Validators.email('ime@mail.example.com'), isNull);
      expect(Validators.email('a.b@sub.sub2.example.co.uk'), isNull);
    });

    test('still rejects genuinely malformed addresses', () {
      expect(Validators.email('ime@'), isNotNull);
      expect(Validators.email('ime@domena'), isNotNull);
      expect(Validators.email('@domena.com'), isNotNull);
      expect(Validators.email('ime domena.com'), isNotNull);
    });
  });

  group('Validators.password', () {
    test('rejects passwords shorter than 8 characters', () {
      expect(Validators.password('abc123'), isNotNull);
    });

    test('rejects passwords without a digit', () {
      expect(Validators.password('samoslova'), isNotNull);
    });

    test('accepts a compliant password', () {
      expect(Validators.password('lozinka1'), isNull);
    });
  });

  group('Validators.confirmPassword', () {
    test('flags a mismatch', () {
      expect(Validators.confirmPassword('drugacija1', 'lozinka1'), 'Lozinke se ne podudaraju.');
    });

    test('passes when both match', () {
      expect(Validators.confirmPassword('lozinka1', 'lozinka1'), isNull);
    });
  });

  group('Validators.username', () {
    test('rejects usernames shorter than 3 characters', () {
      expect(Validators.username('ab'), isNotNull);
    });

    test('rejects invalid characters', () {
      expect(Validators.username('ime prezime!'), isNotNull);
    });

    test('accepts a valid username', () {
      expect(Validators.username('mentor.test_1'), isNull);
    });
  });

  group('Validators.phone', () {
    test('optional field allows empty value', () {
      expect(Validators.phone(''), isNull);
    });

    test('rejects letters', () {
      expect(Validators.phone('abcdefg'), isNotNull);
    });

    test('accepts a valid number', () {
      expect(Validators.phone('+387 61 234 567'), isNull);
    });
  });

  group('Validators.lengthRange', () {
    test('states the expected bounds in the message', () {
      expect(Validators.lengthRange('a', 10, 500, label: 'Biografija'), contains('10'));
      expect(Validators.lengthRange('a', 10, 500, label: 'Biografija'), contains('500'));
    });

    test('accepts a value within range', () {
      expect(Validators.lengthRange('a' * 50, 10, 500, label: 'Biografija'), isNull);
    });
  });

  group('Validators.numberRange', () {
    test('rejects a non-numeric value', () {
      expect(Validators.numberRange('abc', 1, 1000, label: 'Cijena'), isNotNull);
    });

    test('rejects a value outside the allowed range', () {
      expect(Validators.numberRange('5000', 1, 1000, label: 'Cijena'), isNotNull);
    });

    test('accepts a value within range', () {
      expect(Validators.numberRange('99', 1, 1000, label: 'Cijena'), isNull);
    });

    // monthly-price-precision: PUT /api/user-profile/me with monthlyPrice
    // 24.999 was accepted client-side (only [Range(1,1000)] server-side, no
    // scale check), but the decimal(10,2) column silently rounded it to
    // 25.00 while the response echoed back the unrounded 24.999 — a
    // confirmed price that was never actually charged. maxDecimals mirrors
    // the DB's HasPrecision(10,2) so the desktop rejects it before it ever
    // reaches the API.
    group('maxDecimals', () {
      test('rejects a price with more than 2 decimals', () {
        expect(Validators.numberRange('24.999', 1, 1000, label: 'Cijena', maxDecimals: 2), isNotNull);
        expect(Validators.numberRange('24.994', 1, 1000, label: 'Cijena', maxDecimals: 2), isNotNull);
      });

      test('states the limit in Bosnian', () {
        expect(Validators.numberRange('24.999', 1, 1000, label: 'Cijena', maxDecimals: 2), contains('2 decimale'));
      });

      test('accepts a price with exactly 2 decimals', () {
        expect(Validators.numberRange('24.99', 1, 1000, label: 'Cijena', maxDecimals: 2), isNull);
      });

      test('accepts a trailing zero that rounds to the same value', () {
        // A trailing zero adds no precision (24.990 == 24.99), so the
        // backend's decimal.Round(v,2)==v check accepts it — counting
        // characters after the dot would have rejected it.
        expect(Validators.numberRange('24.990', 1, 1000, label: 'Cijena', maxDecimals: 2), isNull);
      });

      test('rejects scientific notation that hides extra decimals', () {
        // '24999e-3' == 24.999: no literal '.' with 3 digits after it, so a
        // character count misses this; parsing the number does not.
        expect(Validators.numberRange('24999e-3', 1, 1000, label: 'Cijena', maxDecimals: 2), isNotNull);
      });

      test('accepts a whole number', () {
        expect(Validators.numberRange('25', 1, 1000, label: 'Cijena', maxDecimals: 2), isNull);
      });

      test('does not apply to integer fields', () {
        expect(Validators.numberRange('5', 0, 60, label: 'Godine iskustva', isInt: true, maxDecimals: 2), isNull);
      });
    });
  });
}
