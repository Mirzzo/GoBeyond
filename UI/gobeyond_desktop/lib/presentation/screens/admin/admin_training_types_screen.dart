import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/services/panel_api_service.dart';
import '../../../core/session/session_controller.dart';
import '../../widgets/panel_card.dart';

class AdminTrainingTypesScreen extends StatefulWidget {
  const AdminTrainingTypesScreen({super.key});
  @override
  State<AdminTrainingTypesScreen> createState() => _AdminTrainingTypesScreenState();
}

class _AdminTrainingTypesScreenState extends State<AdminTrainingTypesScreen> {
  final _service = PanelApiService(ApiClient());
  final _search = TextEditingController();
  List<Map<String, dynamic>> _items = [];
  String? _error;
  bool _loading = true;

  @override
  void initState() { super.initState(); WidgetsBinding.instance.addPostFrameCallback((_) => _load()); }
  @override
  void dispose() { _search.dispose(); super.dispose(); }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final items = await context.read<SessionController>().runAuthenticated(_service.getTrainingTypes);
      if (mounted) setState(() => _items = items);
    } catch (e) { if (mounted) setState(() => _error = e.toString()); }
    finally { if (mounted) setState(() => _loading = false); }
  }

  Future<void> _edit([Map<String, dynamic>? item]) async {
    final name = TextEditingController(text: item?['name']?.toString() ?? '');
    final description = TextEditingController(text: item?['description']?.toString() ?? '');
    final form = GlobalKey<FormState>();
    final accepted = await showDialog<bool>(context: context, builder: (dialogContext) => AlertDialog(
      title: Text(item == null ? 'Add training type' : 'Edit training type'),
      content: SizedBox(width: 420, child: Form(key: form, child: Column(mainAxisSize: MainAxisSize.min, children: [
        TextFormField(controller: name, decoration: const InputDecoration(labelText: 'Name'),
          validator: (v) => (v?.trim().length ?? 0) < 2 ? 'Enter at least 2 characters.' : null),
        TextFormField(controller: description, decoration: const InputDecoration(labelText: 'Description'), maxLines: 3,
          validator: (v) => (v?.trim().length ?? 0) < 4 ? 'Enter at least 4 characters.' : null),
      ]))),
      actions: [TextButton(onPressed: () => Navigator.pop(dialogContext, false), child: const Text('Cancel')),
        FilledButton(onPressed: () { if (form.currentState!.validate()) Navigator.pop(dialogContext, true); }, child: const Text('Save'))],
    ));
    if (accepted == true && mounted) {
      try {
        final payload = {'name': name.text.trim(), 'description': description.text.trim()};
        await context.read<SessionController>().runAuthenticated((token) => item == null
          ? _service.createTrainingType(token, payload)
          : _service.updateTrainingType(token, item['id'] as int, payload));
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(item == null ? 'Training type added.' : 'Training type updated.')));
          await _load();
        }
      } catch (e) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Save failed: $e'))); }
    }
    name.dispose(); description.dispose();
  }

  Future<void> _delete(Map<String, dynamic> item) async {
    final confirmed = await showDialog<bool>(context: context, builder: (dialogContext) => AlertDialog(
      title: const Text('Delete training type?'),
      content: Text('Delete ${item['name']}? This cannot be undone and may be unavailable when mentors use it.'),
      actions: [TextButton(onPressed: () => Navigator.pop(dialogContext, false), child: const Text('Cancel')),
        FilledButton(onPressed: () => Navigator.pop(dialogContext, true), child: const Text('Delete'))],
    ));
    if (confirmed != true || !mounted) return;
    try {
      await context.read<SessionController>().runAuthenticated((token) => _service.deleteTrainingType(token, item['id'] as int));
      if (mounted) { ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Training type deleted.'))); await _load(); }
    } catch (e) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Delete failed: $e'))); }
  }

  @override
  Widget build(BuildContext context) {
    final q = _search.text.trim().toLowerCase();
    final visible = _items.where((item) => '${item['name']} ${item['description']}'.toLowerCase().contains(q)).toList();
    return PanelCard(title: 'Training types', description: 'Manage the categories available to mentors and clients.',
      actions: [IconButton(onPressed: _load, tooltip: 'Refresh', icon: const Icon(Icons.refresh)),
        FilledButton.icon(onPressed: () => _edit(), icon: const Icon(Icons.add), label: const Text('Add type'))],
      child: Column(children: [
        TextField(controller: _search, onChanged: (_) => setState(() {}), decoration: const InputDecoration(labelText: 'Search training types', prefixIcon: Icon(Icons.search))),
        const SizedBox(height: 16),
        if (_loading) const CircularProgressIndicator() else if (_error != null) Text(_error!, style: const TextStyle(color: Colors.redAccent))
        else if (visible.isEmpty) const Text('No matching training types.')
        else ...visible.map((item) => ListTile(title: Text(item['name']?.toString() ?? ''), subtitle: Text(item['description']?.toString() ?? ''),
          trailing: Wrap(children: [IconButton(tooltip: 'Edit', onPressed: () => _edit(item), icon: const Icon(Icons.edit)),
            IconButton(tooltip: 'Delete', onPressed: () => _delete(item), icon: const Icon(Icons.delete_outline))]))),
      ]));
  }
}
