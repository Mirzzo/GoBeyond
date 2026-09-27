import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

import '../../core/network/api_client.dart';
import '../../core/services/auth_api_service.dart';

class MentorRegisterScreen extends StatefulWidget {
  const MentorRegisterScreen({super.key});
  @override
  State<MentorRegisterScreen> createState() => _MentorRegisterScreenState();
}

class _MentorRegisterScreenState extends State<MentorRegisterScreen> {
  final _service = AuthApiService(ApiClient());
  final _form = GlobalKey<FormState>();
  final _first = TextEditingController();
  final _last = TextEditingController();
  final _email = TextEditingController();
  final _password = TextEditingController();
  final _bio = TextEditingController();
  final _age = TextEditingController();
  final _price = TextEditingController();
  PlatformFile? _certificate;
  List<Map<String, dynamic>> _types = [];
  int? _typeId;
  bool _busy = false;
  String? _error;

  @override
  void initState() { super.initState(); _loadTypes(); }
  @override
  void dispose() { for (final c in [_first, _last, _email, _password, _bio, _age, _price]) { c.dispose(); } super.dispose(); }

  Future<void> _loadTypes() async {
    try {
      final types = await _service.trainingTypes();
      if (mounted) setState(() { _types = types; _typeId = types.isEmpty ? null : types.first['id'] as int; });
    } catch (e) { if (mounted) setState(() => _error = 'Training types could not be loaded: $e'); }
  }

  Future<void> _pickCertificate() async {
    final selected = await FilePicker.pickFile(type: FileType.custom, allowedExtensions: ['pdf', 'png', 'jpg', 'jpeg']);
    if (selected == null || !mounted) return;
    if ((await selected.length() ?? 0) > 10000000) { setState(() => _error = 'Certificate must be at most 10 MB.'); return; }
    setState(() { _certificate = selected; _error = null; });
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    if (_certificate?.path == null) { setState(() => _error = 'Choose a PDF or image certificate.'); return; }
    if (_typeId == null) { setState(() => _error = 'Choose a training type.'); return; }
    setState(() { _busy = true; _error = null; });
    try {
      final certificate = await _service.uploadMentorCertificate(_certificate!.path!, _certificate!.name);
      final selectedType = _types.firstWhere((v) => v['id'] == _typeId);
      final categoryName = selectedType['name']?.toString() ?? 'Hybrid';
      await _service.registerMentor({
        'firstName': _first.text.trim(), 'lastName': _last.text.trim(), 'email': _email.text.trim(),
        'password': _password.text, 'bio': _bio.text.trim(), 'age': int.parse(_age.text.trim()),
        'category': const ['Weightlifting', 'Calisthenics', 'Hybrid'].contains(categoryName) ? categoryName : 'Hybrid',
        'trainingTypeId': _typeId, 'price': double.parse(_price.text.trim().replaceAll(',', '.')),
        'certificateFileName': certificate['fileName'], 'certificateFileUrl': certificate['url'],
      });
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Registration submitted. An administrator will review your certificate.')));
        Navigator.pop(context);
      }
    } catch (e) { if (mounted) setState(() => _error = 'Registration failed: $e'); }
    finally { if (mounted) setState(() => _busy = false); }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Mentor registration')),
    body: Center(child: ConstrainedBox(constraints: const BoxConstraints(maxWidth: 600), child: SingleChildScrollView(
      padding: const EdgeInsets.all(24), child: Form(key: _form, child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        const Text('Create mentor account', style: TextStyle(fontSize: 26, fontWeight: FontWeight.bold)),
        const SizedBox(height: 12),
        TextFormField(controller: _first, decoration: const InputDecoration(labelText: 'First name'), validator: _name),
        TextFormField(controller: _last, decoration: const InputDecoration(labelText: 'Last name'), validator: _name),
        TextFormField(controller: _email, decoration: const InputDecoration(labelText: 'Email'), validator: (v) => RegExp(r'^[^\s@]+@[^\s@]+\.[^\s@]+$').hasMatch(v?.trim() ?? '') ? null : 'Enter a valid email address.'),
        TextFormField(controller: _password, obscureText: true, decoration: const InputDecoration(labelText: 'Password'), validator: (v) => (v?.length ?? 0) < 8 ? 'Use at least 8 characters.' : null),
        TextFormField(controller: _age, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Age'), validator: (v) { final n = int.tryParse(v ?? ''); return n == null || n < 18 || n > 100 ? 'Use an age from 18 to 100.' : null; }),
        TextFormField(controller: _price, keyboardType: const TextInputType.numberWithOptions(decimal: true), decoration: const InputDecoration(labelText: 'Monthly price (BAM)'), validator: (v) { final n = double.tryParse((v ?? '').replaceAll(',', '.')); return n == null || n <= 0 || n > 10000 ? 'Use a price from 0.01 to 10000 BAM.' : null; }),
        DropdownButtonFormField<int>(initialValue: _typeId, decoration: const InputDecoration(labelText: 'Training type'),
          items: _types.map((v) => DropdownMenuItem(value: v['id'] as int, child: Text(v['name']?.toString() ?? ''))).toList(),
          onChanged: (v) => setState(() => _typeId = v), validator: (v) => v == null ? 'Select a training type.' : null),
        TextFormField(controller: _bio, maxLines: 3, decoration: const InputDecoration(labelText: 'Biography'), validator: (v) => (v?.trim().length ?? 0) < 20 ? 'Describe your experience in at least 20 characters.' : null),
        const SizedBox(height: 16),
        OutlinedButton.icon(onPressed: _busy ? null : _pickCertificate, icon: const Icon(Icons.attach_file), label: Text(_certificate?.name ?? 'Choose certificate (PDF or image)')),
        if (_certificate != null) Text('Selected: ${_certificate!.name}'),
        if (_error != null) Padding(padding: const EdgeInsets.only(top: 10), child: Text(_error!, style: const TextStyle(color: Colors.redAccent))),
        const SizedBox(height: 20),
        FilledButton(onPressed: _busy ? null : _submit, child: Text(_busy ? 'Submitting...' : 'Submit registration')),
      ])),
    ))),
  );

  String? _name(String? value) => (value?.trim().length ?? 0) < 2 ? 'Enter at least 2 characters.' : null;
}
