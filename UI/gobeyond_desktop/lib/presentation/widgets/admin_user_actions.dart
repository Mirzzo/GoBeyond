import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/network/api_client.dart';
import '../../core/services/panel_api_service.dart';
import '../../core/session/session_controller.dart';

class AdminUserActions extends StatelessWidget {
  AdminUserActions({super.key, required this.user, required this.onChanged});

  final Map<String, dynamic> user;
  final VoidCallback onChanged;
  final PanelApiService _service = PanelApiService(ApiClient());

  Future<void> _edit(BuildContext context) async {
    final fullName = (user['fullName'] ?? '').toString().trim().split(RegExp(r'\s+'));
    final first = TextEditingController(text: fullName.isEmpty ? '' : fullName.first);
    final last = TextEditingController(text: fullName.length < 2 ? '' : fullName.skip(1).join(' '));
    final email = TextEditingController(text: user['email']?.toString() ?? '');
    final form = GlobalKey<FormState>();
    var role = user['role']?.toString() ?? 'Client';
    final saved = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(builder: (context, setDialogState) => AlertDialog(
        title: const Text('Edit account'),
        content: SizedBox(width: 460, child: Form(key: form, child: Column(mainAxisSize: MainAxisSize.min, children: [
          TextFormField(controller: first, decoration: const InputDecoration(labelText: 'First name'), validator: _nameValidator),
          TextFormField(controller: last, decoration: const InputDecoration(labelText: 'Last name'), validator: _nameValidator),
          TextFormField(controller: email, decoration: const InputDecoration(labelText: 'Email'), validator: (v) => RegExp(r'^[^\s@]+@[^\s@]+\.[^\s@]+$').hasMatch(v?.trim() ?? '') ? null : 'Enter a valid email address.'),
          DropdownButtonFormField<String>(initialValue: role, decoration: const InputDecoration(labelText: 'Role'),
            items: const ['Client', 'Mentor', 'Admin'].map((v) => DropdownMenuItem(value: v, child: Text(v))).toList(),
            onChanged: (v) => setDialogState(() => role = v ?? role)),
        ]))),
        actions: [
          TextButton(onPressed: () => Navigator.pop(dialogContext, false), child: const Text('Cancel')),
          FilledButton(onPressed: () { if (form.currentState!.validate()) Navigator.pop(dialogContext, true); }, child: const Text('Save')),
        ],
      )),
    );
    if (saved == true && context.mounted) {
      try {
        await context.read<SessionController>().runAuthenticated((token) => _service.updateUser(token, user['userId'] as int, {
          'firstName': first.text.trim(), 'lastName': last.text.trim(), 'email': email.text.trim(), 'role': role,
        }));
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Account updated.')));
          onChanged();
        }
      } catch (e) { if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Update failed: $e'))); }
    }
    first.dispose(); last.dispose(); email.dispose();
  }

  String? _nameValidator(String? value) => (value?.trim().length ?? 0) < 2 ? 'Enter at least 2 characters.' : null;

  Future<void> _reset(BuildContext context) async {
    final password = TextEditingController();
    final form = GlobalKey<FormState>();
    final confirmed = await showDialog<bool>(context: context, builder: (dialogContext) => AlertDialog(
      title: const Text('Reset password?'),
      content: SizedBox(width: 420, child: Form(key: form, child: Column(mainAxisSize: MainAxisSize.min, children: [
        Text('Set a new password for ${user['fullName']}. This will replace the current password.'),
        TextFormField(controller: password, obscureText: true, decoration: const InputDecoration(labelText: 'New password'),
          validator: (v) => (v?.length ?? 0) < 8 ? 'Use at least 8 characters.' : null),
      ]))),
      actions: [TextButton(onPressed: () => Navigator.pop(dialogContext, false), child: const Text('Cancel')),
        FilledButton(onPressed: () { if (form.currentState!.validate()) Navigator.pop(dialogContext, true); }, child: const Text('Reset'))],
    ));
    if (confirmed == true && context.mounted) {
      try {
        await context.read<SessionController>().runAuthenticated((token) => _service.resetPassword(token, user['userId'] as int, password.text));
        if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Password changed.')));
      } catch (e) { if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Reset failed: $e'))); }
    }
    password.dispose();
  }

  Future<void> _toggleBlock(BuildContext context) async {
    final active = user['isActive'] == true;
    final confirmed = await showDialog<bool>(context: context, builder: (dialogContext) => AlertDialog(
      title: Text(active ? 'Block account?' : 'Unblock account?'),
      content: Text('${user['fullName']} ${active ? 'will lose access to the app.' : 'will regain access to the app.'}'),
      actions: [TextButton(onPressed: () => Navigator.pop(dialogContext, false), child: const Text('Cancel')),
        FilledButton(onPressed: () => Navigator.pop(dialogContext, true), child: Text(active ? 'Block' : 'Unblock'))],
    ));
    if (confirmed != true || !context.mounted) return;
    try {
      await context.read<SessionController>().runAuthenticated((token) => active ? _service.blockUser(token, user['userId'] as int) : _service.unblockUser(token, user['userId'] as int));
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(active ? 'Account blocked.' : 'Account unblocked.')));
        onChanged();
      }
    } catch (e) { if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Action failed: $e'))); }
  }

  @override
  Widget build(BuildContext context) => Wrap(spacing: 4, children: [
    TextButton(onPressed: () => _edit(context), child: const Text('Edit')),
    TextButton(onPressed: () => _reset(context), child: const Text('Reset password')),
    TextButton(onPressed: () => _toggleBlock(context), child: Text(user['isActive'] == true ? 'Block' : 'Unblock')),
  ]);
}
