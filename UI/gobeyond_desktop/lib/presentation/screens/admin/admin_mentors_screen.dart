import 'package:flutter/material.dart';

import '../../../core/services/admin_service.dart';
import '../../../core/services/reference_data_service.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/api_error.dart';
import '../../widgets/avatar.dart';
import '../../widgets/dialogs.dart';
import '../../widgets/panel.dart';
import 'widgets/mentor_report_dialog.dart';
import 'widgets/reset_password_dialog.dart';
import 'widgets/user_edit_dialog.dart';

/// Mockup 02 — AKTIVNI MENTORI.
class AdminMentorsScreen extends StatefulWidget {
  const AdminMentorsScreen({super.key});

  @override
  State<AdminMentorsScreen> createState() => _AdminMentorsScreenState();
}

class _AdminMentorsScreenState extends State<AdminMentorsScreen> {
  final _service = AdminService();
  final _referenceDataService = ReferenceDataService();
  final _searchController = TextEditingController();
  bool _loading = true;
  List<Map<String, dynamic>> _mentors = const [];
  List<Map<String, dynamic>> _trainingTypes = const [];
  int? _trainingTypeFilter;
  bool? _isActiveFilter;

  @override
  void initState() {
    super.initState();
    _referenceDataService.list(ReferenceResource.trainingTypes).then((value) {
      if (mounted) setState(() => _trainingTypes = value);
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
      final mentors = await _service.getMentors(
        search: _searchController.text,
        trainingTypeId: _trainingTypeFilter,
        isActive: _isActiveFilter,
      );
      if (!mounted) return;
      setState(() {
        _mentors = mentors;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _openFilter() async {
    int? trainingTypeId = _trainingTypeFilter;
    bool? isActive = _isActiveFilter;
    await showGbDialog<void>(
      context: context,
      title: 'Filter',
      width: 420,
      child: StatefulBuilder(
        builder: (context, setLocalState) => Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            DropdownButtonFormField<int?>(
              initialValue: trainingTypeId,
              decoration: const InputDecoration(labelText: 'Vrsta treninga'),
              items: [
                const DropdownMenuItem(value: null, child: Text('Sve vrste treninga')),
                ..._trainingTypes.map((t) => DropdownMenuItem(value: t['id'] as int, child: Text(t['name'] as String))),
              ],
              onChanged: (value) => setLocalState(() => trainingTypeId = value),
            ),
            const SizedBox(height: 16),
            DropdownButtonFormField<bool?>(
              initialValue: isActive,
              decoration: const InputDecoration(labelText: 'Status'),
              items: const [
                DropdownMenuItem(value: null, child: Text('Svi statusi')),
                DropdownMenuItem(value: true, child: Text('Aktivan')),
                DropdownMenuItem(value: false, child: Text('Blokiran')),
              ],
              onChanged: (value) => setLocalState(() => isActive = value),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () {
            setState(() {
              _trainingTypeFilter = null;
              _isActiveFilter = null;
            });
            Navigator.of(context).pop();
            _load();
          },
          child: const Text('Poništi'),
        ),
        const SizedBox(width: 8),
        ElevatedButton(
          onPressed: () {
            setState(() {
              _trainingTypeFilter = trainingTypeId;
              _isActiveFilter = isActive;
            });
            Navigator.of(context).pop();
            _load();
          },
          child: const Text('PRIMIJENI'),
        ),
      ],
    );
  }

  Future<void> _delete(Map<String, dynamic> mentor) async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Obriši mentora',
      message:
          'Da li ste sigurni da želite obrisati mentora ${mentor['fullName']}? Njegovi planovi ostaju dostupni pretplatnicima, ali se neće ažurirati, a sve njegove pretplate će biti prekinute i klijenti se više neće naplaćivati.',
      confirmLabel: 'OBRIŠI',
      danger: true,
    );
    if (!confirmed) return;
    try {
      await _service.deleteUser(mentor['userId'] as int);
      if (!mounted) return;
      showSuccessSnack(context, 'Mentor ${mentor['fullName']} je obrisan.');
      _load();
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _toggleBlock(Map<String, dynamic> mentor) async {
    final isActive = mentor['isActive'] == true;
    final confirmed = await showConfirmDialog(
      context,
      title: isActive ? 'Blokiraj mentora' : 'Odblokiraj mentora',
      message: isActive
          ? 'Da li ste sigurni da želite blokirati mentora ${mentor['fullName']}?'
          : 'Da li ste sigurni da želite odblokirati mentora ${mentor['fullName']}?',
      confirmLabel: isActive ? 'BLOKIRAJ' : 'ODBLOKIRAJ',
      danger: isActive,
    );
    if (!confirmed) return;
    try {
      if (isActive) {
        await _service.blockUser(mentor['userId'] as int);
      } else {
        await _service.unblockUser(mentor['userId'] as int);
      }
      if (!mounted) return;
      showSuccessSnack(context, isActive ? 'Mentor ${mentor['fullName']} je blokiran.' : 'Mentor ${mentor['fullName']} je odblokiran.');
      _load();
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  @override
  Widget build(BuildContext context) {
    return ContentPanel(
      title: 'AKTIVNI MENTORI',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(children: [
            Expanded(
              child: SearchField(controller: _searchController, hintText: 'Pretraga mentora', onSubmitted: (_) => _load()),
            ),
            const SizedBox(width: 12),
            PillButton(label: 'FILTER', icon: Icons.filter_list, onPressed: _openFilter),
          ]),
          const SizedBox(height: 16),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _mentors.isEmpty
                    ? const EmptyState(message: 'Nema mentora koji odgovaraju pretrazi.')
                    : ListView.separated(
                        itemCount: _mentors.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 14),
                        itemBuilder: (context, index) {
                          final mentor = _mentors[index];
                          final isActive = mentor['isActive'] == true;
                          return Container(
                            padding: const EdgeInsets.all(18),
                            decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(18)),
                            child: Row(
                              crossAxisAlignment: CrossAxisAlignment.center,
                              children: [
                                GbAvatar(imageUrl: mentor['profileImageUrl'] as String?, size: 84),
                                const SizedBox(width: 20),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text('IME: ${mentor['fullName']}', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 16)),
                                      const SizedBox(height: 4),
                                      Text('VRSTA TRENINGA: ${(mentor['trainingTypeName'] as String? ?? '').toUpperCase()}',
                                          style: const TextStyle(color: Colors.white)),
                                      const SizedBox(height: 4),
                                      Row(children: [
                                        Icon(Icons.star, color: AppColors.accent, size: 16),
                                        const SizedBox(width: 4),
                                        Text('${mentor['averageRating'] ?? 0} (${mentor['reviewCount'] ?? 0})', style: const TextStyle(color: AppColors.textMuted)),
                                        const SizedBox(width: 16),
                                        const Icon(Icons.people, color: AppColors.textMuted, size: 16),
                                        const SizedBox(width: 4),
                                        Text('${mentor['activeSubscribers'] ?? 0} pretplatnika', style: const TextStyle(color: AppColors.textMuted)),
                                        const SizedBox(width: 16),
                                        StatusChip(label: isActive ? 'Aktivan' : 'Blokiran', color: isActive ? AppColors.success : AppColors.danger),
                                      ]),
                                    ],
                                  ),
                                ),
                                Wrap(
                                  spacing: 10,
                                  runSpacing: 10,
                                  alignment: WrapAlignment.end,
                                  children: [
                                    PillButton(
                                      label: 'UREDI',
                                      dense: true,
                                      onPressed: () async {
                                        final changed = await showUserEditDialog(context, userId: mentor['userId'] as int);
                                        if (changed == true) _load();
                                      },
                                    ),
                                    PillButton(
                                      label: 'IZVJEŠTAJ',
                                      dense: true,
                                      onPressed: () => showMentorReportDialog(context,
                                          mentorProfileId: mentor['mentorProfileId'] as int, fullName: mentor['fullName'] as String? ?? ''),
                                    ),
                                    PillButton(
                                      label: isActive ? 'BLOKIRAJ' : 'ODBLOKIRAJ',
                                      dense: true,
                                      onPressed: () => _toggleBlock(mentor),
                                    ),
                                    PillButton(
                                      label: 'RESETUJ LOZINKU',
                                      dense: true,
                                      onPressed: () => showResetPasswordDialog(context,
                                          userId: mentor['userId'] as int, fullName: mentor['fullName'] as String? ?? ''),
                                    ),
                                    PillButton(
                                      label: 'OBRIŠI',
                                      dense: true,
                                      color: AppColors.danger,
                                      onPressed: () => _delete(mentor),
                                    ),
                                  ],
                                ),
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
