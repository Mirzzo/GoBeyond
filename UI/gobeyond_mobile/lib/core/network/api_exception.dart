import 'package:dio/dio.dart';

/// Normalized error thrown by every repository call so screens only ever
/// have to handle one exception shape, matching the backend error contract:
/// `{ message, errors?: { field: [messages] } }`.
class ApiException implements Exception {
  ApiException(this.message, {this.errors = const {}, this.statusCode});

  final String message;
  final Map<String, List<String>> errors;
  final int? statusCode;

  /// First server-side validation message for [field], if any.
  String? fieldError(String field) {
    final list = errors[field];
    if (list == null || list.isEmpty) return null;
    return list.first;
  }

  bool get isConnectivityError => statusCode == null;

  factory ApiException.from(Object error) {
    if (error is ApiException) return error;

    if (error is DioException) {
      final response = error.response;
      final data = response?.data;

      if (data is Map) {
        final message = data['message']?.toString();
        final rawErrors = data['errors'];
        final errors = <String, List<String>>{};
        if (rawErrors is Map) {
          rawErrors.forEach((key, value) {
            if (value is List) {
              errors[key.toString()] = value.map((e) => e.toString()).toList();
            } else if (value != null) {
              errors[key.toString()] = [value.toString()];
            }
          });
        }

        return ApiException(
          message ?? _fallbackMessage(response?.statusCode),
          errors: errors,
          statusCode: response?.statusCode,
        );
      }

      return ApiException(
        _fallbackMessage(response?.statusCode),
        statusCode: response?.statusCode,
      );
    }

    return ApiException('Došlo je do neočekivane greške. Pokušajte ponovo.');
  }

  static String _fallbackMessage(int? statusCode) {
    if (statusCode == null) {
      return 'Nema veze sa serverom. Provjerite internet konekciju i API adresu.';
    }
    if (statusCode == 401) {
      return 'Sesija je istekla. Prijavite se ponovo.';
    }
    if (statusCode >= 500) {
      return 'Došlo je do greške na serveru. Pokušajte ponovo.';
    }
    return 'Zahtjev nije uspio. Pokušajte ponovo.';
  }

  @override
  String toString() => message;
}
