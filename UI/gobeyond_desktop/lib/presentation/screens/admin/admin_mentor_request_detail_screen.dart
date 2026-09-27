import 'package:flutter/material.dart';

import '../../../core/services/admin_service.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/api_error.dart';
import '../../../core/utils/formatters.dart';
import '../../widgets/certificate_viewer.dart';
import '../../widgets/dialogs.dart';
import '../../widgets/panel.dart';

/// Dedicated PREGLED screen for a mentor request (mockup 01 + prijava 3.1.1):
/// shows the full profile description and lets the admin review each
/// certificate in-app (images inline, PDFs rendered with PdfPreview) before
/// deciding ODOBRI / ODBIJ.
class AdminMentorRequestDetailScreen extends StatefulWidget {
  const AdminMentorRequestDetailScreen({super.key, required this.mentorProfileId});

  final int mentorProfileId;

  @override
  State<AdminMentorRequestDetailScreen> createState() => _AdminMentorRequestDetailScreenState();
}

class _AdminMentorRequestDetailScreenState extends State<AdminMentorRequestDetailScreen> {
  final _service = AdminService();
  bool _loading = true;
  bool _acting = false;
  String? _error;
  Map<String, dynamic>? _detail;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final detail = await _service.getMentorRequestDetail(widget.mentorProfileId);
      if (!mounted) return;
      setState(() {
        _detail = detail;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = ApiError.from(error).message;
      });
    }
  }

  Future<void> _verify(Map<String, dynamic> certificate) async {
    try {
      final updated = await _service.verifyCertificate(certificate['id'] as int);
      if (!mounted) return;
      setState(() {
        final certs = List<Map<String, dynamic>>.from(_detail!['certificates'] as List);
        final index = certs.indexWhere((c) => c['id'] == certificate['id']);
        if (index != -1) certs[index] = updated;
        _detail!['certificates'] = certs;
      });
      showSuccessSnack(context, 'Certifikat je verifikovan.');
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _approve() async {
    final detail = _detail!;
    final fullName = detail['fullName'] as String? ?? '';
    final confirmed = await showConfirmDialog(
      context,
      title: 'Odobri mentora',
      message: 'Da li ste sigurni da želite odobriti mentorski nalog za $fullName?',
      confirmLabel: 'ODOBRI',
    );
    if (!confirmed) return;
    setState(() => _acting = true);
    try {
      final message = await _service.approveMentorRequest(widget.mentorProfileId);
      if (!mounted) return;
      showSuccessSnack(context, message);
      Navigator.of(context).pop(true);
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    } finally {
      if (mounted) setState(() => _acting = false);
    }
  }

  Future<void> _reject() async {
    final detail = _detail!;
    final fullName = detail['fullName'] as String? ?? '';
    final reason = await showReasonDialog(
      context,
      title: 'Odbij zahtjev za mentora',
      label: 'Razlog odbijanja (10-500 znakova)',
      warning: 'Korisnik $fullName će biti obaviješten o odbijanju sa navedenim razlogom.',
    );
    if (reason == null) return;
    setState(() => _acting = true);
    try {
      final message = await _service.rejectMentorRequest(widget.mentorProfileId, reason);
      if (!mounted) return;
      showSuccessSnack(context, message);
      Navigator.of(context).pop(true);
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    } finally {
      if (mounted) setState(() => _acting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Pregled zahtjeva za mentora'), backgroundColor: AppColors.panel),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Text(_error!, style: const TextStyle(color: AppColors.danger)))
              : _buildBody(_detail!),
    );
  }

  Widget _buildBody(Map<String, dynamic> detail) {
    final certificates = (detail['certificates'] as List<dynamic>? ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList();
    final specializations = (detail['specializationNames'] as List<dynamic>? ?? const []).join(', ');

    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.all(20),
            decoration: BoxDecoration(color: AppColors.panel, borderRadius: BorderRadius.circular(18)),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(detail['fullName'] as String? ?? '', style: const TextStyle(color: Colors.white, fontSize: 22, fontWeight: FontWeight.bold)),
                const SizedBox(height: 4),
                Text('AKA ${detail['nickname'] ?? '-'}', style: const TextStyle(color: AppColors.textMuted)),
                const SizedBox(height: 16),
                Wrap(spacing: 24, runSpacing: 12, children: [
                  _infoItem('Email', detail['email'] as String? ?? '-'),
                  _infoItem('Telefon', detail['phoneNumber'] as String? ?? '-'),
                  _infoItem('Godine', '${detail['age'] ?? '-'}'),
                  _infoItem('Vrsta treninga', detail['trainingTypeName'] as String? ?? '-'),
                  _infoItem('Godine iskustva', '${detail['yearsOfExperience'] ?? '-'}'),
                  _infoItem('Mjesečna cijena', Formatters.money(detail['monthlyPrice'] as num?)),
                  _infoItem('Zahtjev poslan', Formatters.dateTime(detail['requestedAt'] as String?)),
                ]),
                const SizedBox(height: 16),
                const Text('Specijalizacije', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold)),
                const SizedBox(height: 4),
                Text(specializations.isEmpty ? '-' : specializations, style: const TextStyle(color: Colors.white)),
                const SizedBox(height: 16),
                const Text('Biografija', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold)),
                const SizedBox(height: 4),
                Text(detail['bio'] as String? ?? '-', style: const TextStyle(color: Colors.white, height: 1.4)),
              ],
            ),
          ),
          const SizedBox(height: 20),
          const Text('Certifikati struke', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 16)),
          const SizedBox(height: 12),
          ...certificates.map((certificate) => _CertificateViewer(certificate: certificate, onVerify: () => _verify(certificate))),
          const SizedBox(height: 28),
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: _acting ? null : _reject,
                  style: OutlinedButton.styleFrom(foregroundColor: AppColors.danger, side: const BorderSide(color: AppColors.danger)),
                  child: const Text('ODBIJ'),
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: ElevatedButton(
                  onPressed: _acting ? null : _approve,
                  child: const Text('ODOBRI'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _infoItem(String label, String value) {
    return SizedBox(
      width: 220,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
          Text(value, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600)),
        ],
      ),
    );
  }
}

class _CertificateViewer extends StatefulWidget {
  const _CertificateViewer({required this.certificate, required this.onVerify});

  final Map<String, dynamic> certificate;
  final VoidCallback onVerify;

  @override
  State<_CertificateViewer> createState() => _CertificateViewerState();
}

class _CertificateViewerState extends State<_CertificateViewer> {
  bool _expanded = false;

  @override
  Widget build(BuildContext context) {
    final certificate = widget.certificate;
    final fileUrl = certificate['fileUrl'] as String?;
    final fileName = certificate['fileName'] as String? ?? '';
    final isVerified = certificate['isVerified'] == true;

    return Container(
      margin: const EdgeInsets.only(bottom: 14),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(color: AppColors.panel, borderRadius: BorderRadius.circular(16)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(fileName.toLowerCase().endsWith('.pdf') ? Icons.picture_as_pdf : Icons.image, color: AppColors.accent),
              const SizedBox(width: 10),
              Expanded(
                child: Text(fileName, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600)),
              ),
              StatusChip(
                label: isVerified ? 'Verifikovan' : 'Nije verifikovan',
                color: isVerified ? AppColors.success : AppColors.textMuted,
              ),
              const SizedBox(width: 10),
              TextButton(onPressed: () => setState(() => _expanded = !_expanded), child: Text(_expanded ? 'Sakrij' : 'Prikaži')),
              if (!isVerified)
                TextButton(onPressed: widget.onVerify, child: const Text('Verifikuj certifikat')),
            ],
          ),
          if (_expanded && fileUrl != null) ...[
            const SizedBox(height: 12),
            CertificateViewer(fileUrl: fileUrl, fileName: fileName),
          ],
        ],
      ),
    );
  }
}
