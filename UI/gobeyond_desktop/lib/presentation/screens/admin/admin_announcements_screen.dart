import 'package:flutter/material.dart';

import '../../../core/models/app_role.dart';
import '../../../core/services/admin_service.dart';
import '../../../core/services/reference_data_service.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/api_error.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/utils/server_errors.dart';
import '../../../core/utils/validators.dart';
import '../../widgets/dialogs.dart';
import '../../widgets/panel.dart';

/// SISTEMSKE OBAVIJESTI — admin-authored announcements broadcast to users.
class AdminAnnouncementsScreen extends StatefulWidget {
  const AdminAnnouncementsScreen({super.key});

  @override
  State<AdminAnnouncementsScreen> createState() => _AdminAnnouncementsScreenState();
}

class _AdminAnnouncementsScreenState extends State<AdminAnnouncementsScreen> {
  final _service = AdminService();
  final _referenceDataService = ReferenceDataService();
  final _searchController = TextEditingController();
  bool _loading = true;
  List<Map<String, dynamic>> _announcements = const [];
  List<Map<String, dynamic>> _roles = const [];

  @override
  void initState() {
    super.initState();
    _referenceDataService.roles().then((value) {
      if (mounted) setState(() => _roles = value);
    });
    _load();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final announcements = await _service.getAnnouncements(search: _searchController.text);
      if (!mounted) return;
      setState(() {
        _announcements = announcements;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _openForm({Map<String, dynamic>? existing}) async {
    final saved = await showAnnouncementDialog(context, service: _service, roles: _roles, existing: existing);

    if (saved == true) {
      if (!mounted) return;
      showSuccessSnack(context, existing != null ? 'Obavijest je ažurirana.' : 'Sistemska obavijest je objavljena.');
      _load();
    }
  }

  Future<void> _delete(Map<String, dynamic> announcement) async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Obriši obavijest',
      message: 'Da li ste sigurni da želite obrisati obavijest "${announcement['title']}"?',
      confirmLabel: 'OBRIŠI',
      danger: true,
    );
    if (!confirmed) return;
    try {
      await _service.deleteAnnouncement(announcement['id'] as int);
      if (!mounted) return;
      showSuccessSnack(context, 'Obavijest je obrisana.');
      _load();
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  @override
  Widget build(BuildContext context) {
    return ContentPanel(
      title: 'SISTEMSKE OBAVIJESTI',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(children: [
            Expanded(child: SearchField(controller: _searchController, hintText: 'Pretraga po naslovu', onSubmitted: (_) => _load())),
            const SizedBox(width: 12),
            ElevatedButton.icon(onPressed: () => _openForm(), icon: const Icon(Icons.add), label: const Text('NOVA OBAVIJEST')),
          ]),
          const SizedBox(height: 16),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _announcements.isEmpty
                    ? const EmptyState(message: 'Nema sistemskih obavijesti.')
                    : ListView.separated(
                        itemCount: _announcements.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 10),
                        itemBuilder: (context, index) {
                          final announcement = _announcements[index];
                          return Container(
                            padding: const EdgeInsets.all(16),
                            decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(14)),
                            child: Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text(announcement['title'] as String? ?? '', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 15)),
                                      const SizedBox(height: 4),
                                      Text(announcement['content'] as String? ?? '', style: const TextStyle(color: AppColors.textMuted)),
                                      const SizedBox(height: 8),
                                      Wrap(spacing: 14, children: [
                                        StatusChip(
                                          label: announcement['targetRole'] != null
                                              ? AppRole.fromWire(announcement['targetRole']).displayName
                                              : 'Svi korisnici',
                                          color: AppColors.info,
                                        ),
                                        Text('Primalaca: ${announcement['recipientCount'] ?? 0}', style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
                                        Text(Formatters.dateTime(announcement['createdAt'] as String?), style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
                                      ]),
                                    ],
                                  ),
                                ),
                                PillButton(label: 'UREDI', dense: true, onPressed: () => _openForm(existing: announcement)),
                                const SizedBox(width: 8),
                                PillButton(label: 'OBRIŠI', dense: true, color: AppColors.danger, onPressed: () => _delete(announcement)),
                              ],
                            ),
                          );
                        },
                      ),
          ),
        ],
      ),
    );
  }
}

/// The NOVA/UREDI OBAVIJEST form. Its own [StatefulWidget] so the title and
/// content [TextEditingController]s are created in `initState` and disposed
/// in `dispose` — called only once the dialog route is actually removed,
/// unlike disposing them by hand right after `await showDialog(...)`
/// returns, which raced the dialog's exit transition and threw "A
/// TextEditingController was used after being disposed."
Future<bool?> showAnnouncementDialog(
  BuildContext context, {
  required AdminService service,
  required List<Map<String, dynamic>> roles,
  Map<String, dynamic>? existing,
}) {
  return showDialog<bool>(
    context: context,
    builder: (_) => _AnnouncementDialog(service: service, roles: roles, existing: existing),
  );
}

class _AnnouncementDialog extends StatefulWidget {
  const _AnnouncementDialog({required this.service, required this.roles, this.existing});

  final AdminService service;
  final List<Map<String, dynamic>> roles;
  final Map<String, dynamic>? existing;

  @override
  State<_AnnouncementDialog> createState() => _AnnouncementDialogState();
}

class _AnnouncementDialogState extends State<_AnnouncementDialog> {
  final _formKey = GlobalKey<FormState>();
  final _serverErrors = ServerErrors();
  late final _titleController = TextEditingController(text: widget.existing?['title'] as String? ?? '');
  late final _contentController = TextEditingController(text: widget.existing?['content'] as String? ?? '');
  String? _targetRole;

  bool get _isEdit => widget.existing != null;

  @override
  void initState() {
    super.initState();
    _targetRole = widget.existing?['targetRole'] as String?;
  }

  @override
  void dispose() {
    _titleController.dispose();
    _contentController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    try {
      if (_isEdit) {
        await widget.service.updateAnnouncement(
            widget.existing!['id'] as int, _titleController.text.trim(), _contentController.text.trim());
      } else {
        await widget.service.createAnnouncement(_titleController.text.trim(), _contentController.text.trim(), _targetRole);
      }
      if (!mounted) return;
      Navigator.of(context).pop(true);
    } catch (error) {
      if (!mounted) return;
      final apiError = ApiError.from(error, fallback: 'Čuvanje nije uspjelo.');
      setState(() => _serverErrors.apply(apiError.fieldErrors));
      _formKey.currentState!.validate();
      showErrorSnack(context, apiError.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    return GbDialog(
      title: _isEdit ? 'Uredi sistemsku obavijest' : 'Nova sistemska obavijest',
      width: 520,
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Odustani')),
        const SizedBox(width: 8),
        ElevatedButton(onPressed: _submit, child: Text(_isEdit ? 'SAČUVAJ' : 'OBJAVI')),
      ],
      child: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextFormField(
              controller: _titleController,
              decoration: const InputDecoration(labelText: 'Naslov'),
              validator: _serverErrors.wrap('title', (v) => Validators.lengthRange(v, 3, 120, label: 'Naslov')),
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _contentController,
              maxLines: 5,
              decoration: const InputDecoration(labelText: 'Sadržaj'),
              validator: _serverErrors.wrap('content', (v) => Validators.lengthRange(v, 10, 2000, label: 'Sadržaj')),
            ),
            const SizedBox(height: 14),
            DropdownButtonFormField<String?>(
              initialValue: _targetRole,
              decoration: const InputDecoration(labelText: 'Ciljna grupa'),
              items: [
                const DropdownMenuItem(value: null, child: Text('Svi korisnici')),
                ...widget.roles.map((r) => DropdownMenuItem(value: r['value'] as String, child: Text(r['name'] as String))),
              ],
              onChanged: _isEdit ? null : (value) => setState(() => _targetRole = value),
            ),
            if (_isEdit)
              const Padding(
                padding: EdgeInsets.only(top: 6),
                child: Align(
                  alignment: Alignment.centerLeft,
                  child: Text('Ciljna grupa se ne može mijenjati nakon slanja.', style: TextStyle(color: AppColors.textMuted, fontSize: 12)),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
