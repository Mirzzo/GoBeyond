import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/utils/validators.dart';

void main() {
  group('Validators required() agrees with the label\'s gender', () {
    test('neuter label (default) reads "je obavezno"', () {
      expect(Validators.required(''), 'Ovo polje je obavezno.');
    });

    test('feminine label reads "je obavezna"', () {
      expect(
        Validators.required('', label: 'Lozinka', gender: LabelGender.feminine),
        'Lozinka je obavezna.',
      );
    });

    test('masculine label reads "je obavezan"', () {
      expect(
        Validators.required('',
            label: 'Komentar', gender: LabelGender.masculine),
        'Komentar je obavezan.',
      );
    });

    test('plural label reads "su obavezni"', () {
      expect(
        Validators.required('', label: 'Obimi', gender: LabelGender.plural),
        'Obimi su obavezni.',
      );
    });
  });

  group('Validators textLength() agrees with the label\'s gender', () {
    test('feminine label: empty value', () {
      expect(
        Validators.textLength('',
            min: 2, max: 300, label: 'Snaga', gender: LabelGender.feminine),
        'Snaga je obavezna (2–300 znakova).',
      );
    });

    test('masculine label: empty value', () {
      expect(
        Validators.textLength('',
            min: 10,
            max: 1000,
            label: 'Komentar',
            gender: LabelGender.masculine),
        'Komentar je obavezan (10–1000 znakova).',
      );
    });

    test('plural label uses "moraju" for the out-of-range message', () {
      expect(
        Validators.textLength('a',
            min: 2, max: 300, label: 'Obimi', gender: LabelGender.plural),
        'Obimi moraju imati između 2 i 300 znakova.',
      );
    });

    test('plural label reads "su obavezni" for the empty message', () {
      expect(
        Validators.textLength('',
            min: 2, max: 300, label: 'Obimi', gender: LabelGender.plural),
        'Obimi su obavezni (2–300 znakova).',
      );
    });

    test('singular genders use "mora" for the out-of-range message', () {
      expect(
        Validators.textLength('a',
            min: 2, max: 300, label: 'Snaga', gender: LabelGender.feminine),
        'Snaga mora imati između 2 i 300 znakova.',
      );
    });

    test(
        'default (neuter) still matches the existing "Ime" registration message',
        () {
      expect(
        Validators.textLength('', min: 2, max: 50, label: 'Ime'),
        'Ime je obavezno (2–50 znakova).',
      );
    });
  });

  group('Validators numberRange() agrees with the label\'s gender', () {
    test('masculine label: empty value', () {
      expect(
        Validators.numberRange('',
            min: 1,
            max: 10000,
            label: 'Broj ponavljanja',
            gender: LabelGender.masculine),
        'Broj ponavljanja je obavezan.',
      );
    });

    test('feminine label: empty value', () {
      expect(
        Validators.numberRange('',
            min: 30, max: 300, label: 'Težina', gender: LabelGender.feminine),
        'Težina je obavezna.',
      );
    });

    test('plural label uses "su" and "moraju"', () {
      expect(
        Validators.numberRange('',
            min: 1, max: 10, label: 'Setovi', gender: LabelGender.plural),
        'Setovi su obavezni.',
      );
      expect(
        Validators.numberRange('20',
            min: 1, max: 10, label: 'Setovi', gender: LabelGender.plural),
        'Setovi moraju biti između 1 i 10.',
      );
      expect(
        Validators.numberRange('x',
            min: 1,
            max: 10,
            label: 'Setovi',
            isInt: true,
            gender: LabelGender.plural),
        'Setovi moraju biti cijeli brojevi.',
      );
    });

    test('singular labels keep "mora" for the out-of-range message', () {
      expect(
        Validators.numberRange('500',
            min: 30, max: 300, label: 'Težina', gender: LabelGender.feminine),
        'Težina mora biti između 30 i 300.',
      );
    });
  });
}
