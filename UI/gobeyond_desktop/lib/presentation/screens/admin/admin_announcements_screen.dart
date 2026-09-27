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
    final formKey = GlobalKey<FormState>();
    final serverErrors = ServerErrors();
    final titleController = TextEditingController(text: existing?['title'] as String? ?? '');
    final contentController = TextEditingController(text: existing?['content'] as String? ?? '');
    String? targetRole = existing?['targetRole'] as String?;
    final isEdit = existing != null;

    final saved = await showGbDialog<bool>(
      context: context,
      title: isEdit ? 'Uredi sistemsku obavijest' : 'Nova sistemska obavijest',
      width: 520,
      child: StatefulBuilder(
        builder: (context, setLocalState) => Form(
          key: formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextFormField(
                controller: titleController,
                decoration: const InputDecoration(labelText: 'Naslov'),
                validator: serverErrors.wrap('title', (v) => Validators.lengthRange(v, 3, 120, label: 'Naslov')),
              ),
              const SizedBox(height: 14),
              TextFormField(
                controller: contentController,
                maxLines: 5,
                decoration: const InputDecoration(labelText: 'Sadržaj'),
                validator: serverErrors.wrap('content', (v) => Validators.lengthRange(v, 10, 2000, label: 'Sadržaj')),
              ),
              const SizedBox(height: 14),
              DropdownButtonFormField<String?>(
                initialValue: targetRole,
                decoration: const InputDecoration(labelText: 'Ciljna grupa'),
                items: [
                  const DropdownMenuItem(value: null, child: Text('Svi korisnici')),
                  ..._roles.map((r) => DropdownMenuItem(value: r['value'] as String, child: Text(r['name'] as String))),
                ],
                onChanged: isEdit ? null : (value) => setLocalState(() => targetRole = value),
              ),
              if (isEdit)
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
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Odustani')),
        const SizedBox(width: 8),
        ElevatedButton(
          onPressed: () async {
            if (!formKey.currentState!.validate()) return;
            try {
              if (isEdit) {
                await _service.updateAnnouncement(existing['id'] as int, titleController.text.trim(), contentController.text.trim());
              } else {
                await _service.createAnnouncement(titleController.text.trim(), contentController.text.trim(), targetRole);
              }
              if (!mounted) return;
              Navigator.of(context).pop(true);
            } catch (error) {
              final apiError = ApiError.from(error, fallback: 'Čuvanje nije uspjelo.');
              serverErrors.apply(apiError.fieldErrors);
              formKey.currentState!.validate();
              if (!mounted) return;
              showErrorSnack(context, apiError.message);
            }
          },
          child: Text(isEdit ? 'SAČUVAJ' : 'OBJAVI'),
        ),
      ],
    );

    titleController.dispose();
    contentController.dispose();

    if (saved == true) {
      if (!mounted) return;
      showSuccessSnack(context, isEdit ? 'Obavijest je ažurirana.' : 'Sistemska obavijest je objavljena.');
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
