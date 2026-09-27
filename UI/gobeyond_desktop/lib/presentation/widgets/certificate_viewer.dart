import 'package:flutter/material.dart';
import 'package:printing/printing.dart';

import '../../core/theme/app_theme.dart';
import '../../core/utils/api_error.dart';
import '../../core/utils/authenticated_file.dart';

/// In-app certificate preview: downloads the (now private) file through
/// [AuthenticatedFileService] and renders it as [PdfPreview] or
/// [Image.memory] depending on the resolved content type, with a loading
/// spinner and a Bosnian error state. Used by both the admin mentor-request
/// review screen and the mentor's own profile certificate list, so the
/// fetch-and-decide logic lives in exactly one place.
class CertificateViewer extends StatefulWidget {
  const CertificateViewer({super.key, required this.fileUrl, required this.fileName, this.height = 480});

  final String fileUrl;
  final String fileName;
  final double height;

  @override
  State<CertificateViewer> createState() => _CertificateViewerState();
}

class _CertificateViewerState extends State<CertificateViewer> {
  late Future<AuthenticatedFile> _future;

  @override
  void initState() {
    super.initState();
    _future = AuthenticatedFileService.fetch(widget.fileUrl, fileNameHint: widget.fileName);
  }

  Future<void> _retry() async {
    setState(() {
      _future = AuthenticatedFileService.fetch(widget.fileUrl, fileNameHint: widget.fileName);
    });
  }

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: widget.height,
      child: FutureBuilder<AuthenticatedFile>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError || !snapshot.hasData) {
            final message = snapshot.hasError
                ? ApiError.from(snapshot.error!, fallback: 'Nije moguće učitati certifikat.').message
                : 'Nije moguće učitati certifikat.';
            return Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.error_outline, color: AppColors.danger, size: 32),
                  const SizedBox(height: 8),
                  Text(message, style: const TextStyle(color: AppColors.danger), textAlign: TextAlign.center),
                  const SizedBox(height: 12),
                  OutlinedButton(onPressed: _retry, child: const Text('Pokušaj ponovo')),
                ],
              ),
            );
          }

          final file = snapshot.data!;
          if (file.isPdf) {
            return PdfPreview(
              build: (format) async => file.bytes,
              canChangeOrientation: false,
              canChangePageFormat: false,
              canDebug: false,
              allowPrinting: false,
              allowSharing: false,
              useActions: false,
            );
          }
          return Image.memory(
            file.bytes,
            fit: BoxFit.contain,
            errorBuilder: (_, _, _) => const Center(
              child: Text('Nije moguće prikazati sliku.', style: TextStyle(color: AppColors.danger)),
            ),
          );
        },
      ),
    );
  }
}
