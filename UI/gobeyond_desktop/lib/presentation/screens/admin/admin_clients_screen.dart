import 'package:flutter/material.dart';

import '../../../core/services/admin_service.dart';
import '../../../core/services/reference_data_service.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/api_error.dart';
import '../../widgets/avatar.dart';
import '../../widgets/dialogs.dart';
import '../../widgets/panel.dart';
import 'widgets/client_report_dialog.dart';
import 'widgets/reset_password_dialog.dart';
import 'widgets/user_edit_dialog.dart';

/// KLIJENTI — same pattern as Mentori (search + filter by fitness goal/status).
class AdminClientsScreen extends StatefulWidget {
  const AdminClientsScreen({super.key});

  @override
  State<AdminClientsScreen> createState() => _AdminClientsScreenState();
}

class _AdminClientsScreenState extends State<AdminClientsScreen> {
  final _service = AdminService();
  final _referenceDataService = ReferenceDataService();
  final _searchController = TextEditingController();
  bool _loading = true;
  List<Map<String, dynamic>> _clients = const [];
  List<Map<String, dynamic>> _fitnessGoals = const [];
  int? _fitnessGoalFilter;
  bool? _isActiveFilter;

  @override
  void initState() {
    super.initState();
    _referenceDataService.list(ReferenceResource.fitnessGoals).then((value) {
      if (mounted) setState(() => _fitnessGoals = value);
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
      final clients = await _service.getClients(
        search: _searchController.text,
        fitnessGoalId: _fitnessGoalFilter,
        isActive: _isActiveFilter,
      );
      if (!mounted) return;
      setState(() {
        _clients = clients;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _openFilter() async {
    int? fitnessGoalId = _fitnessGoalFilter;
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
              initialValue: fitnessGoalId,
              decoration: const InputDecoration(labelText: 'Cilj'),
              items: [
                const DropdownMenuItem(value: null, child: Text('Svi ciljevi')),
                ..._fitnessGoals.map((g) => DropdownMenuItem(value: g['id'] as int, child: Text(g['name'] as String))),
              ],
              onChanged: (value) => setLocalState(() => fitnessGoalId = value),
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
              _fitnessGoalFilter = null;
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
              _fitnessGoalFilter = fitnessGoalId;
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

  Future<void> _delete(Map<String, dynamic> client) async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Obriši klijenta',
      message: 'Da li ste sigurni da želite obrisati klijenta ${client['fullName']}? Ova akcija se ne može poništiti.',
      confirmLabel: 'OBRIŠI',
      danger: true,
    );
    if (!confirmed) return;
    try {
      await _service.deleteUser(client['userId'] as int);
      if (!mounted) return;
      showSuccessSnack(context, 'Klijent ${client['fullName']} je obrisan.');
      _load();
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _toggleBlock(Map<String, dynamic> client) async {
    final isActive = client['isActive'] == true;
    final confirmed = await showConfirmDialog(
      context,
      title: isActive ? 'Blokiraj klijenta' : 'Odblokiraj klijenta',
      message: isActive
          ? 'Da li ste sigurni da želite blokirati klijenta ${client['fullName']}?'
          : 'Da li ste sigurni da želite odblokirati klijenta ${client['fullName']}?',
      confirmLabel: isActive ? 'BLOKIRAJ' : 'ODBLOKIRAJ',
      danger: isActive,
    );
    if (!confirmed) return;
    try {
      if (isActive) {
        await _service.blockUser(client['userId'] as int);
      } else {
        await _service.unblockUser(client['userId'] as int);
      }
      if (!mounted) return;
      showSuccessSnack(context, isActive ? 'Klijent ${client['fullName']} je blokiran.' : 'Klijent ${client['fullName']} je odblokiran.');
      _load();
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  @override
  Widget build(BuildContext context) {
    return ContentPanel(
      title: 'KLIJENTI',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(children: [
            Expanded(child: SearchField(controller: _searchController, hintText: 'Pretraga klijenata', onSubmitted: (_) => _load())),
            const SizedBox(width: 12),
            PillButton(label: 'FILTER', icon: Icons.filter_list, onPressed: _openFilter),
          ]),
          const SizedBox(height: 16),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _clients.isEmpty
                    ? const EmptyState(message: 'Nema klijenata koji odgovaraju pretrazi.')
                    : ListView.separated(
                        itemCount: _clients.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 14),
                        itemBuilder: (context, index) {
                          final client = _clients[index];
                          final isActive = client['isActive'] == true;
                          return Container(
                            padding: const EdgeInsets.all(18),
                            decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(18)),
                            child: Row(
                              crossAxisAlignment: CrossAxisAlignment.center,
                              children: [
                                GbAvatar(imageUrl: client['profileImageUrl'] as String?, size: 84),
                                const SizedBox(width: 20),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text('IME: ${client['fullName']}', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 16)),
                                      const SizedBox(height: 4),
                                      Text('CILJ: ${(client['fitnessGoalName'] as String? ?? '-').toUpperCase()}', style: const TextStyle(color: Colors.white)),
                                      const SizedBox(height: 4),
                                      Row(children: [
                                        Text('Nivo: ${client['fitnessLevelName'] ?? '-'}', style: const TextStyle(color: AppColors.textMuted)),
                                        const SizedBox(width: 16),
                                        Text('Mentor: ${client['activeMentorName'] ?? 'Nema'}', style: const TextStyle(color: AppColors.textMuted)),
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
                                        final changed = await showUserEditDialog(context, userId: client['userId'] as int);
                                        if (changed == true) _load();
                                      },
                                    ),
                                    PillButton(
                                      label: 'IZVJEŠTAJ',
                                      dense: true,
                                      onPressed: () => showClientReportDialog(context,
                                          clientProfileId: client['clientProfileId'] as int, fullName: client['fullName'] as String? ?? ''),
                                    ),
                                    PillButton(
                                      label: isActive ? 'BLOKIRAJ' : 'ODBLOKIRAJ',
                                      dense: true,
                                      onPressed: () => _toggleBlock(client),
                                    ),
                                    PillButton(
                                      label: 'RESETUJ LOZINKU',
                                      dense: true,
                                      onPressed: () => showResetPasswordDialog(context,
                                          userId: client['userId'] as int, fullName: client['fullName'] as String? ?? ''),
                                    ),
                                    PillButton(
                                      label: 'OBRIŠI',
                                      dense: true,
                                      color: AppColors.danger,
                                      onPressed: () => _delete(client),
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
