import 'package:flutter_test/flutter_test.dart';
import 'package:gobeyond_desktop/core/utils/authenticated_file.dart';

/// Certificates are now served from an authenticated, extension-less route
/// (`/api/certificates/{id}/file`), so the PDF-vs-image decision can no
/// longer come from the URL — it has to come from the response
/// `Content-Type`, with the original file name as a fallback.
void main() {
  group('AuthenticatedFileService.resolveIsPdf — Content-Type is authoritative', () {
    test('application/pdf is a PDF', () {
      expect(AuthenticatedFileService.resolveIsPdf('application/pdf', 'anything'), isTrue);
    });

    test('a Content-Type with charset/params still matches pdf', () {
      expect(AuthenticatedFileService.resolveIsPdf('application/pdf; charset=binary', 'anything'), isTrue);
    });

    test('image/png is not a PDF, regardless of file name', () {
      expect(AuthenticatedFileService.resolveIsPdf('image/png', 'certificate.pdf'), isFalse);
    });

    test('image/jpeg is not a PDF', () {
      expect(AuthenticatedFileService.resolveIsPdf('image/jpeg', 'photo.jpeg'), isFalse);
    });
  });

  group('AuthenticatedFileService.resolveIsPdf — falls back to the file name', () {
    test('missing Content-Type falls back to a .pdf name', () {
      expect(AuthenticatedFileService.resolveIsPdf(null, 'diploma.pdf'), isTrue);
    });

    test('generic octet-stream Content-Type falls back to a .pdf name', () {
      expect(AuthenticatedFileService.resolveIsPdf('application/octet-stream', 'diploma.PDF'), isTrue);
    });

    test('generic octet-stream Content-Type with a non-pdf name is treated as an image', () {
      expect(AuthenticatedFileService.resolveIsPdf('application/octet-stream', 'photo.jpg'), isFalse);
    });

    test('missing Content-Type and non-pdf name is treated as an image', () {
      expect(AuthenticatedFileService.resolveIsPdf(null, 'certificate.png'), isFalse);
    });
  });
}
