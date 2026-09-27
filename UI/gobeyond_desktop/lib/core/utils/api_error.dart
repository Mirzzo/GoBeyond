import 'package:dio/dio.dart';

/// Parsed shape of the backend's error envelope (see api-contract.md section 0):
/// `{ "message": "...", "errors": { "field": ["..."] } }`.
class ApiError {
  const ApiError({required this.message, this.fieldErrors = const {}});

  final String message;
  final Map<String, String> fieldErrors;

  static ApiError from(Object error, {String fallback = 'Došlo je do greške. Pokušajte ponovo.'}) {
    if (error is DioException) {
      final data = error.response?.data;
      if (data is Map) {
        final map = Map<String, dynamic>.from(data);
        final message = (map['message'] as String?)?.trim();
        final fieldErrors = <String, String>{};
        final errors = map['errors'];
        if (errors is Map) {
          errors.forEach((key, value) {
            if (value is List && value.isNotEmpty) {
              fieldErrors[key.toString()] = value.first.toString();
            } else if (value is String) {
              fieldErrors[key.toString()] = value;
            }
          });
        }
        if (message != null && message.isNotEmpty) {
          return ApiError(message: message, fieldErrors: fieldErrors);
        }
        if (fieldErrors.isNotEmpty) {
          return ApiError(message: 'Provjerite unesene podatke.', fieldErrors: fieldErrors);
        }
      }
      if (error.type == DioExceptionType.connectionError ||
          error.type == DioExceptionType.connectionTimeout) {
        return const ApiError(
          message: 'Nije moguće uspostaviti vezu sa serverom. Provjerite konekciju i pokušajte ponovo.',
        );
      }
    }
    return ApiError(message: fallback);
  }
}
