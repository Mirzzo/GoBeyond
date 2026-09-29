import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/models/app_role.dart';
import '../../../core/services/admin_service.dart';
import '../../../core/services/reference_data_service.dart';
import '../../../core/session/session_controller.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/api_error.dart';
import '../../widgets/avatar.dart';
import '../../widgets/dialogs.dart';
import '../../widgets/panel.dart';
import 'widgets/reset_password_dialog.dart';
import 'widgets/user_edit_dialog.dart';

/// KORISNICI — svi korisnici (admin/mentor/klijent). Dodjela i izmjena
/// korisničkih uloga (prijava 3.1.1).
class AdminUsersScreen extends StatefulWidget {
  const AdminUsersScreen({super.key});

  @override
  State<AdminUsersScreen> createState() => _AdminUsersScreenState();
}

class _AdminUsersScreenState extends State<AdminUsersScreen> {
  final _service = AdminService();
  final _referenceDataService = ReferenceDataService();
  final _searchController = TextEditingController();
  bool _loading = true;
  List<Map<String, dynamic>> _users = const [];
  List<Map<String, dynamic>> _roles = const [];
  String? _roleFilter;

  @override
  void initState() {
    super.initState();
    _referenceDataService.roles().then((value) {
      if (mounted) setState(() => _roles = value);
    }).catchError((error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
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
      final users = await _service.getUsers(search: _searchController.text, role: _roleFilter);
      if (!mounted) return;
      setState(() {
        _users = users;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _delete(Map<String, dynamic> user) async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Obriši korisnika',
      message: 'Da li ste sigurni da želite obrisati korisnika ${user['fullName']}? Ova akcija se ne može poništiti.',
      confirmLabel: 'OBRIŠI',
      danger: true,
    );
    if (!confirmed) return;
    try {
      final warning = await _service.deleteUser(user['id'] as int);
      if (!mounted) return;
      showSuccessSnack(context, 'Korisnik ${user['fullName']} je obrisan.');
      _load();
      await showPaymentWarningIfAny(context, warning);
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _toggleBlock(Map<String, dynamic> user) async {
    final isActive = user['isActive'] == true;
    final confirmed = await showConfirmDialog(
      context,
      title: isActive ? 'Blokiraj korisnika' : 'Odblokiraj korisnika',
      message: isActive
          ? 'Da li ste sigurni da želite blokirati korisnika ${user['fullName']}?'
          : 'Da li ste sigurni da želite odblokirati korisnika ${user['fullName']}?',
      confirmLabel: isActive ? 'BLOKIRAJ' : 'ODBLOKIRAJ',
      danger: isActive,
    );
    if (!confirmed) return;
    try {
      if (isActive) {
        await _service.blockUser(user['id'] as int);
      } else {
        await _service.unblockUser(user['id'] as int);
      }
      if (!mounted) return;
      showSuccessSnack(context, isActive ? 'Korisnik ${user['fullName']} je blokiran.' : 'Korisnik ${user['fullName']} je odblokiran.');
      _load();
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final currentUserId = context.watch<SessionController>().user?.id;
    return ContentPanel(
      title: 'KORISNICI',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(children: [
            Expanded(child: SearchField(controller: _searchController, hintText: 'Pretraga po imenu, korisničkom imenu ili emailu', onSubmitted: (_) => _load())),
            const SizedBox(width: 12),
            SizedBox(
              width: 220,
              child: DropdownButtonFormField<String?>(
                initialValue: _roleFilter,
                decoration: const InputDecoration(labelText: 'Uloga'),
                items: [
                  const DropdownMenuItem(value: null, child: Text('Sve uloge')),
                  ..._roles.map((r) => DropdownMenuItem(value: r['value'] as String, child: Text(r['name'] as String))),
                ],
                onChanged: (value) {
                  setState(() => _roleFilter = value);
                  _load();
                },
              ),
            ),
          ]),
          const SizedBox(height: 16),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _users.isEmpty
                    ? const EmptyState(message: 'Nema korisnika koji odgovaraju pretrazi.')
                    : ListView.separated(
                        itemCount: _users.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 10),
                        itemBuilder: (context, index) {
                          final user = _users[index];
                          final isActive = user['isActive'] == true;
                          final isSelf = user['id'] == currentUserId;
                          return Container(
                            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                            decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(14)),
                            child: Row(
                              children: [
                                GbAvatar(imageUrl: user['profileImageUrl'] as String?, size: 52),
                                const SizedBox(width: 14),
                                Expanded(
                                  flex: 3,
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text(user['fullName'] as String? ?? '', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                                      Text('@${user['username']} · ${user['email']}', style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
                                    ],
                                  ),
                                ),
                                Expanded(
                                  flex: 1,
                                  child: StatusChip(label: AppRole.fromWire(user['role']).displayName, color: AppColors.info),
                                ),
                                Expanded(
                                  flex: 1,
                                  child: StatusChip(label: isActive ? 'Aktivan' : 'Blokiran', color: isActive ? AppColors.success : AppColors.danger),
                                ),
                                Wrap(
                                  spacing: 8,
                                  children: [
                                    PillButton(
                                      label: 'UREDI',
                                      dense: true,
                                      onPressed: () async {
                                        final changed = await showUserEditDialog(context, userId: user['id'] as int, isSelf: isSelf);
                                        if (changed == true) _load();
                                      },
                                    ),
                                    Tooltip(
                                      message: isSelf ? 'Ne možete blokirati vlastiti nalog.' : '',
                                      child: PillButton(
                                        label: isActive ? 'BLOKIRAJ' : 'ODBLOKIRAJ',
                                        dense: true,
                                        onPressed: isSelf ? null : () => _toggleBlock(user),
                                      ),
                                    ),
                                    PillButton(
                                      label: 'RESETUJ LOZINKU',
                                      dense: true,
                                      onPressed: () => showResetPasswordDialog(context, userId: user['id'] as int, fullName: user['fullName'] as String? ?? ''),
                                    ),
                                    Tooltip(
                                      message: isSelf ? 'Ne možete obrisati vlastiti nalog.' : '',
                                      child: PillButton(
                                        label: 'OBRIŠI',
                                        dense: true,
                                        color: AppColors.danger,
                                        onPressed: isSelf ? null : () => _delete(user),
                                      ),
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
