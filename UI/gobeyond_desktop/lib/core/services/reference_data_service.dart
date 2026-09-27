import 'package:dio/dio.dart';

import '../network/api_client.dart';

enum ReferenceResource {
  trainingTypes('training-types'),
  fitnessGoals('fitness-goals'),
  fitnessLevels('fitness-levels'),
  genders('genders');

  const ReferenceResource(this.path);
  final String path;
}

/// Generic BaseCRUD reference-data client (api-contract.md section 3).
/// Used for all four šifarnici resources plus `/api/roles`.
class ReferenceDataService {
  Dio get _dio => ApiClient.instance.dio;

  Future<List<Map<String, dynamic>>> list(ReferenceResource resource, {String? name}) async {
    // api-contract.md Changelog v1.1: pageSize is capped 1-100 (default 50)
    // server-side, so we page through in batches of 100 instead of asking
    // for a single oversized page.
    final items = <Map<String, dynamic>>[];
    var page = 1;
    while (true) {
      final response = await _dio.get<Map<String, dynamic>>(
        '/api/${resource.path}',
        queryParameters: {
          if (name != null && name.trim().isNotEmpty) 'name': name.trim(),
          'page': page,
          'pageSize': 100,
        },
      );
      final pageItems = (response.data?['items'] as List<dynamic>? ?? const [])
          .map((e) => Map<String, dynamic>.from(e as Map))
          .toList();
      items.addAll(pageItems);
      final totalCount = response.data?['totalCount'] as int? ?? items.length;
      if (pageItems.isEmpty || items.length >= totalCount) break;
      page++;
    }
    return items;
  }

  Future<Map<String, dynamic>> create(ReferenceResource resource, Map<String, dynamic> payload) async {
    final response = await _dio.post<Map<String, dynamic>>('/api/${resource.path}', data: payload);
    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<Map<String, dynamic>> update(ReferenceResource resource, int id, Map<String, dynamic> payload) async {
    final response = await _dio.put<Map<String, dynamic>>('/api/${resource.path}/$id', data: payload);
    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<void> delete(ReferenceResource resource, int id) async {
    await _dio.delete<void>('/api/${resource.path}/$id');
  }

  Future<List<Map<String, dynamic>>> roles() async {
    final response = await _dio.get<List<dynamic>>('/api/roles');
    return (response.data ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }
}
