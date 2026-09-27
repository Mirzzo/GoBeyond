import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../../core/network/dio_client.dart';
import '../models/progress_model.dart';

class ProgressRepository {
  ProgressRepository(this._client);

  final DioClient _client;

  Future<ProgressHistoryModel> getProgressHistory({String? search}) async {
    final response = await _client.dio.get<Map<String, dynamic>>(
      '/api/progress',
      queryParameters: {
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
      },
    );

    if (response.data == null) {
      throw Exception('Empty progress response.');
    }

    return ProgressHistoryModel.fromJson(response.data!);
  }

  Future<void> createProgressEntry(Map<String, dynamic> payload) async {
    await _client.dio.post<void>('/api/progress', data: payload);
  }

  Future<void> uploadPhoto(String photoUrl) async {
    await _client.dio.post<void>(
      '/api/progress/photo',
      data: {'photoUrl': photoUrl},
    );
  }

  Future<String> uploadPhotoFile(Uint8List bytes, String fileName) async {
    final upload = await _client.dio.post<Map<String, dynamic>>(
      '/api/files/upload',
      data: FormData.fromMap({
        'file': MultipartFile.fromBytes(bytes, filename: fileName),
      }),
    );
    final url = upload.data?['url']?.toString();
    if (url == null || url.isEmpty)
      throw StateError('Server nije vratio adresu slike.');
    await uploadPhoto(url);
    return url;
  }

  Future<Map<String, dynamic>> getPlanForEntry(int entryId) async {
    final response = await _client.dio
        .get<Map<String, dynamic>>('/api/progress/$entryId/plan');
    return response.data ?? const {};
  }
}
