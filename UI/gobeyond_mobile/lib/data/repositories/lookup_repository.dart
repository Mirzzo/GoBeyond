import '../../core/network/dio_client.dart';
import '../models/lookup_item.dart';

/// Reference-table dropdown data (`training-types`, `fitness-goals`,
/// `fitness-levels`, `genders`) — every dropdown in the app is fed from
/// these, never hardcoded, per the course UI rules.
abstract class LookupRepository {
  Future<List<LookupItem>> getTrainingTypes();
  Future<List<LookupItem>> getFitnessGoals();
  Future<List<LookupItem>> getFitnessLevels();
  Future<List<LookupItem>> getGenders();
}

class ApiLookupRepository implements LookupRepository {
  ApiLookupRepository({DioClient? client}) : _client = client ?? DioClient();

  final DioClient _client;

  Future<List<LookupItem>> _getResource(String resource) async {
    final response = await _client.dio.get<Map<String, dynamic>>(
      '/api/$resource',
      // Contract v1.1: pageSize is clamped 1–100 (default 50); these
      // reference tables never have more than a handful of rows anyway.
      queryParameters: {'pageSize': 100},
    );
    final items = response.data?['items'] as List<dynamic>? ?? const [];
    return items
        .whereType<Map<String, dynamic>>()
        .map(LookupItem.fromJson)
        .toList();
  }

  @override
  Future<List<LookupItem>> getTrainingTypes() => _getResource('training-types');

  @override
  Future<List<LookupItem>> getFitnessGoals() => _getResource('fitness-goals');

  @override
  Future<List<LookupItem>> getFitnessLevels() => _getResource('fitness-levels');

  @override
  Future<List<LookupItem>> getGenders() => _getResource('genders');
}
