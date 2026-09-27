import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../../core/network/dio_client.dart';
import '../models/progress_entry.dart';
import '../models/training_plan.dart';

abstract class ProgressRepository {
  Future<List<int>> getYears();
  Future<List<ProgressEntryItem>> getEntries(int year);
  Future<ProgressEntryItem?> getEntry(int year, int month);
  Future<ProgressEntryItem> upsertEntry({
    required int year,
    required int month,
    required num weightKg,
    required String measurements,
    required String strength,
    required String conditioning,
  });
  Future<ProgressEntryItem> uploadPhoto(
      int year, int month, Uint8List bytes, String fileName);
  Future<TrainingPlan?> getPlanSnapshot(int year, int month);
  Future<List<WeightPoint>> getChart();
}

class ApiProgressRepository implements ProgressRepository {
  ApiProgressRepository({DioClient? client}) : _client = client ?? DioClient();

  final DioClient _client;

  @override
  Future<List<int>> getYears() async {
    final response =
        await _client.dio.get<List<dynamic>>('/api/progress/years');
    return (response.data ?? const []).map((e) => e as int).toList();
  }

  @override
  Future<List<ProgressEntryItem>> getEntries(int year) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/progress',
      queryParameters: {'year': year},
    );
    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(ProgressEntryItem.fromJson)
        .toList();
  }

  @override
  Future<ProgressEntryItem?> getEntry(int year, int month) async {
    try {
      final response = await _client.dio
          .get<Map<String, dynamic>>('/api/progress/$year/$month');
      return ProgressEntryItem.fromJson(response.data ?? const {});
    } on DioException catch (error) {
      if (error.response?.statusCode == 404) return null;
      rethrow;
    }
  }

  @override
  Future<ProgressEntryItem> upsertEntry({
    required int year,
    required int month,
    required num weightKg,
    required String measurements,
    required String strength,
    required String conditioning,
  }) async {
    final response = await _client.dio.put<Map<String, dynamic>>(
      '/api/progress/$year/$month',
      data: {
        'weightKg': weightKg,
        'measurements': measurements,
        'strength': strength,
        'conditioning': conditioning,
      },
    );
    return ProgressEntryItem.fromJson(response.data ?? const {});
  }

  @override
  Future<ProgressEntryItem> uploadPhoto(
      int year, int month, Uint8List bytes, String fileName) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/progress/$year/$month/photo',
      data: FormData.fromMap({
        'file': MultipartFile.fromBytes(bytes, filename: fileName),
      }),
    );
    return ProgressEntryItem.fromJson(response.data ?? const {});
  }

  @override
  Future<TrainingPlan?> getPlanSnapshot(int year, int month) async {
    try {
      final response = await _client.dio
          .get<Map<String, dynamic>>('/api/progress/$year/$month/plan');
      return TrainingPlan.fromJson(response.data ?? const {});
    } on DioException catch (error) {
      if (error.response?.statusCode == 404) return null;
      rethrow;
    }
  }

  @override
  Future<List<WeightPoint>> getChart() async {
    final response =
        await _client.dio.get<List<dynamic>>('/api/progress/chart');
    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(WeightPoint.fromJson)
        .toList();
  }
}
