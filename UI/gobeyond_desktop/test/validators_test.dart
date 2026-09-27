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
  });
}
