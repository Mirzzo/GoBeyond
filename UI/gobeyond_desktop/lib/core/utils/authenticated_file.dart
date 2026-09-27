import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../config/app_config.dart';
import '../network/api_client.dart';

/// Result of fetching a private, Bearer-token-protected file (mentor
/// certificates as of api-contract.md — `Certificate.fileUrl` is now
/// `/api/certificates/{id}/file`, served only to Admin or the owning
/// Mentor). Profile/progress photos stay public static URLs and are NOT
/// routed through this helper.
class AuthenticatedFile {
  const AuthenticatedFile({required this.bytes, required this.isPdf});

  final Uint8List bytes;
  final bool isPdf;
}

/// Single shared place to download an authenticated file's bytes through
/// the app's [ApiClient] (so the Bearer token interceptor applies — plain
/// `Image.network`/a bare URL would not send it) and decide PDF vs. image
/// from the response `Content-Type`, falling back to the file name's
/// extension if the header is missing or generic.
class AuthenticatedFileService {
  const AuthenticatedFileService._();

  static Future<AuthenticatedFile> fetch(String urlOrPath, {String? fileNameHint}) async {
    final url = AppConfig.resolveUrl(urlOrPath);
    if (url == null) {
      throw Exception('Nevažeća putanja fajla.');
    }
    final response = await ApiClient.instance.dio.get<List<int>>(
      url,
      options: Options(responseType: ResponseType.bytes),
    );
    final bytes = Uint8List.fromList(response.data ?? const []);
    final contentType = response.headers.value(Headers.contentTypeHeader);
    return AuthenticatedFile(bytes: bytes, isPdf: resolveIsPdf(contentType, fileNameHint ?? urlOrPath));
  }

  /// Decides PDF vs. image from the response `Content-Type`, falling back
  /// to the file name's extension when the header is missing or generic
  /// (e.g. `application/octet-stream`). Public (and pure/side-effect-free)
  /// so it can be unit-tested without mocking the network call.
  static bool resolveIsPdf(String? contentType, String nameHint) {
    final normalizedType = contentType?.toLowerCase() ?? '';
    if (normalizedType.contains('pdf')) return true;
    if (normalizedType.startsWith('image/')) return false;
    return nameHint.toLowerCase().endsWith('.pdf');
  }
}
