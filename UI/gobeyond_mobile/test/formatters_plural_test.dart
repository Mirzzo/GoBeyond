import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_mobile/core/utils/formatters.dart';

void main() {
  test('Formatters.plural follows the Bosnian 1 / 2-4 / 5+ rule', () {
    String reviews(int n) =>
        '$n ${Formatters.plural(n, 'recenzija', 'recenzije', 'recenzija')}';
    String years(int n) =>
        '$n ${Formatters.plural(n, 'godina', 'godine', 'godina')}';
    String days(int n) => '$n ${Formatters.plural(n, 'dan', 'dana', 'dana')}';

    expect(days(0), '0 dana');
    expect(days(1), '1 dan');
    expect(days(2), '2 dana');
    expect(days(5), '5 dana');
    expect(days(11), '11 dana');
    expect(days(21), '21 dan');
    expect(days(111), '111 dana');

    expect(reviews(2), '2 recenzije');
    expect(reviews(4), '4 recenzije');
    expect(reviews(12), '12 recenzija');
    expect(reviews(22), '22 recenzije');
    expect(reviews(25), '25 recenzija');

    expect(years(1), '1 godina');
    expect(years(3), '3 godine');
    expect(years(14), '14 godina');
    expect(years(21), '21 godina');
    expect(years(24), '24 godine');
  });
}
