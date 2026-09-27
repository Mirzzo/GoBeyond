import 'dart:io';

import 'package:dio/dio.dart' show MultipartFile;
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

import '../../core/services/auth_service.dart';
import '../../core/services/reference_data_service.dart';
import '../../core/theme/app_theme.dart';
import '../../core/utils/api_error.dart';
import '../../core/utils/server_errors.dart';
import '../../core/utils/validators.dart';
import '../widgets/dialogs.dart';

const _allowedCertExtensions = ['pdf', 'jpg', 'jpeg', 'png'];
const _maxCertBytes = 5 * 1024 * 1024;
const _maxCertCount = 5;

class MentorRegisterScreen extends StatefulWidget {
  const MentorRegisterScreen({super.key});

  @override
  State<MentorRegisterScreen> createState() => _MentorRegisterScreenState();
}

class _MentorRegisterScreenState extends State<MentorRegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  final _referenceDataService = ReferenceDataService();
  final _authService = AuthService();
  final _serverErrors = ServerErrors();

  final _firstName = TextEditingController();
  final _lastName = TextEditingController();
  final _username = TextEditingController();
  final _email = TextEditingController();
  final _phone = TextEditingController();
  final _nickname = TextEditingController();
  final _bio = TextEditingController();
  final _years = TextEditingController();
  final _price = TextEditingController();
  final _password = TextEditingController();
  final _confirmPassword = TextEditingController();

  DateTime? _dateOfBirth;
  int? _genderId;
  int? _trainingTypeId;
  final Set<int> _specializationIds = {};
  final List<_PickedCertificate> _certificates = [];

  bool _loadingReference = true;
  bool _submitting = false;
  List<Map<String, dynamic>> _genders = const [];
  List<Map<String, dynamic>> _trainingTypes = const [];
  List<Map<String, dynamic>> _fitnessGoals = const [];

  @override
  void initState() {
    super.initState();
    _loadReferenceData();
  }

  Future<void> _loadReferenceData() async {
    try {
      final results = await Future.wait([
        _referenceDataService.list(ReferenceResource.genders),
        _referenceDataService.list(ReferenceResource.trainingTypes),
        _referenceDataService.list(ReferenceResource.fitnessGoals),
      ]);
      setState(() {
        _genders = results[0];
        _trainingTypes = results[1];
        _fitnessGoals = results[2];
        _loadingReference = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() => _loadingReference = false);
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  @override
  void dispose() {
    for (final c in [
      _firstName, _lastName, _username, _email, _phone, _nickname, _bio, _years, _price, _password, _confirmPassword
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _pickDateOfBirth() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: DateTime(now.year - 25, now.month, now.day),
      firstDate: DateTime(now.year - 90),
      lastDate: DateTime(now.year - 16, now.month, now.day),
      helpText: 'Odaberite datum rođenja',
    );
    if (picked != null) {
      setState(() => _dateOfBirth = picked);
    }
  }

  Future<void> _pickCertificates() async {
    if (_certificates.length >= _maxCertCount) {
      showErrorSnack(context, 'Maksimalno $_maxCertCount certifikata.');
      return;
    }
    final result = await FilePicker.pickFiles(
      type: FileType.custom,
      allowedExtensions: _allowedCertExtensions,
    );
    if (result.isEmpty || !mounted) return;

    final remainingSlots = _maxCertCount - _certificates.length;
    final accepted = <_PickedCertificate>[];
    final rejected = <String>[];
    for (final file in result.take(remainingSlots)) {
      if (file.path == null) continue;
      final size = File(file.path!).lengthSync();
      if (size > _maxCertBytes) {
        rejected.add('${file.name} (veći od 5 MB)');
        continue;
      }
      accepted.add(_PickedCertificate(path: file.path!, name: file.name, sizeBytes: size));
    }
    setState(() => _certificates.addAll(accepted));
    if (rejected.isNotEmpty) {
      showErrorSnack(context, 'Odbačeni fajlovi: ${rejected.join(', ')}');
    }
  }

  Future<void> _submit() async {
    _serverErrors.clear();
    final formValid = _formKey.currentState!.validate();
    final missing = <String>[];
    if (_dateOfBirth == null) missing.add('Datum rođenja');
    if (_genderId == null) missing.add('Spol');
    if (_trainingTypeId == null) missing.add('Vrsta treninga');
    if (_specializationIds.isEmpty) missing.add('Specijalizacije');
    if (_certificates.isEmpty) missing.add('Certifikati');

    if (!formValid || missing.isNotEmpty) {
      setState(() {});
      if (missing.isNotEmpty) {
        showErrorSnack(context, 'Popunite obavezna polja: ${missing.join(', ')}.');
      }
      return;
    }

    setState(() => _submitting = true);
    try {
      final dob = _dateOfBirth!;
      final dobString =
          '${dob.year.toString().padLeft(4, '0')}-${dob.month.toString().padLeft(2, '0')}-${dob.day.toString().padLeft(2, '0')}';

      final certificates = await Future.wait(
        _certificates.map((c) => MultipartFile.fromFile(c.path, filename: c.name)),
      );

      final message = await _authService.registerMentor(
        fields: {
          'firstName': _firstName.text.trim(),
          'lastName': _lastName.text.trim(),
          'username': _username.text.trim(),
          'email': _email.text.trim(),
          if (_phone.text.trim().isNotEmpty) 'phoneNumber': _phone.text.trim(),
          'dateOfBirth': dobString,
          'genderId': _genderId.toString(),
          'password': _password.text,
          'confirmPassword': _confirmPassword.text,
          'trainingTypeId': _trainingTypeId.toString(),
          if (_nickname.text.trim().isNotEmpty) 'nickname': _nickname.text.trim(),
          'bio': _bio.text.trim(),
          'yearsOfExperience': _years.text.trim(),
          'monthlyPrice': _price.text.trim(),
        },
        specializationIds: _specializationIds.toList(),
        certificates: certificates,
      );

      if (!mounted) return;
      await showGbDialog<void>(
        context: context,
        title: 'Registracija poslana',
        barrierDismissible: false,
        child: Text(message, style: const TextStyle(color: Colors.white, fontSize: 15)),
        actions: [
          ElevatedButton(
            onPressed: () {
              Navigator.of(context).pop();
              Navigator.of(context).pop();
            },
            child: const Text('U redu'),
          ),
        ],
      );
    } catch (error) {
      final apiError = ApiError.from(error, fallback: 'Registracija nije uspjela. Pokušajte ponovo.');
      setState(() => _serverErrors.apply(apiError.fieldErrors));
      _formKey.currentState!.validate();
      if (mounted) showErrorSnack(context, apiError.message);
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Registracija mentora'),
        backgroundColor: AppColors.panel,
      ),
      body: _loadingReference
          ? const Center(child: CircularProgressIndicator())
          : Center(
              child: SingleChildScrollView(
                padding: const EdgeInsets.all(24),
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 720),
                  child: Container(
                    padding: const EdgeInsets.all(28),
                    decoration: BoxDecoration(color: AppColors.panel, borderRadius: BorderRadius.circular(20)),
                    child: Form(
                      key: _formKey,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Text('Podaci o korisniku',
                              style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 18)),
                          const SizedBox(height: 16),
                          Row(children: [
                            Expanded(
                              child: TextFormField(
                                controller: _firstName,
                                decoration: const InputDecoration(labelText: 'Ime'),
                                validator: _serverErrors.wrap(
                                    'firstName', (v) => Validators.lengthRange(v, 2, 50, label: 'Ime')),
                              ),
                            ),
                            const SizedBox(width: 16),
                            Expanded(
                              child: TextFormField(
                                controller: _lastName,
                                decoration: const InputDecoration(labelText: 'Prezime'),
                                validator: _serverErrors.wrap(
                                    'lastName', (v) => Validators.lengthRange(v, 2, 50, label: 'Prezime')),
                              ),
                            ),
                          ]),
                          const SizedBox(height: 16),
                          Row(children: [
                            Expanded(
                              child: TextFormField(
                                controller: _username,
                                decoration: const InputDecoration(labelText: 'Korisničko ime'),
                                validator: _serverErrors.wrap('username', Validators.username),
                              ),
                            ),
                            const SizedBox(width: 16),
                            Expanded(
                              child: TextFormField(
                                controller: _email,
                                decoration: const InputDecoration(labelText: 'Email'),
                                validator: _serverErrors.wrap('email', Validators.email),
                              ),
                            ),
                          ]),
                          const SizedBox(height: 16),
                          Row(children: [
                            Expanded(
                              child: TextFormField(
                                controller: _phone,
                                decoration: const InputDecoration(labelText: 'Telefon (opciono)'),
                                validator: _serverErrors.wrap('phoneNumber', (v) => Validators.phone(v)),
                              ),
                            ),
                            const SizedBox(width: 16),
                            Expanded(
                              child: FormField<DateTime>(
                                initialValue: _dateOfBirth,
                                validator: (value) =>
                                    _serverErrors.forField('dateOfBirth') ?? (value == null ? 'Datum rođenja je obavezan.' : null),
                                builder: (state) => InkWell(
                                  onTap: () async {
                                    await _pickDateOfBirth();
                                    state.didChange(_dateOfBirth);
                                  },
                                  child: InputDecorator(
                                    decoration: InputDecoration(
                                      labelText: 'Datum rođenja',
                                      errorText: state.errorText,
                                      suffixIcon: const Icon(Icons.calendar_today, size: 18),
                                    ),
                                    child: Text(
                                      _dateOfBirth == null
                                          ? 'Odaberite datum'
                                          : '${_dateOfBirth!.day.toString().padLeft(2, '0')}.${_dateOfBirth!.month.toString().padLeft(2, '0')}.${_dateOfBirth!.year}.',
                                      style: TextStyle(color: _dateOfBirth == null ? AppColors.textMuted : Colors.white),
                                    ),
                                  ),
                                ),
                              ),
                            ),
                          ]),
                          const SizedBox(height: 16),
                          DropdownButtonFormField<int>(
                            initialValue: _genderId,
                            decoration: const InputDecoration(labelText: 'Spol'),
                            items: _genders
                                .map((g) => DropdownMenuItem(value: g['id'] as int, child: Text(g['name'] as String)))
                                .toList(),
                            onChanged: (value) => setState(() => _genderId = value),
                            validator: (value) =>
                                _serverErrors.forField('genderId') ?? Validators.requiredSelection(value, label: 'Spol'),
                          ),
                          const SizedBox(height: 24),
                          const Text('Podaci o mentoru',
                              style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 18)),
                          const SizedBox(height: 16),
                          DropdownButtonFormField<int>(
                            initialValue: _trainingTypeId,
                            decoration: const InputDecoration(labelText: 'Vrsta treninga'),
                            items: _trainingTypes
                                .map((t) => DropdownMenuItem(value: t['id'] as int, child: Text(t['name'] as String)))
                                .toList(),
                            onChanged: (value) => setState(() => _trainingTypeId = value),
                            validator: (value) => _serverErrors.forField('trainingTypeId') ??
                                Validators.requiredSelection(value, label: 'Vrsta treninga'),
                          ),
                          const SizedBox(height: 16),
                          TextFormField(
                            controller: _nickname,
                            decoration: const InputDecoration(labelText: 'Nadimak / AKA (opciono)'),
                            validator: _serverErrors.wrap(
                                'nickname', (v) => Validators.optionalLengthRange(v, 2, 50, label: 'Nadimak')),
                          ),
                          const SizedBox(height: 16),
                          TextFormField(
                            controller: _bio,
                            maxLines: 4,
                            decoration: const InputDecoration(labelText: 'Biografija (50-4000 znakova)'),
                            validator: _serverErrors.wrap(
                                'bio', (v) => Validators.lengthRange(v, 50, 4000, label: 'Biografija')),
                          ),
                          const SizedBox(height: 16),
                          Row(children: [
                            Expanded(
                              child: TextFormField(
                                controller: _years,
                                keyboardType: TextInputType.number,
                                decoration: const InputDecoration(labelText: 'Godine iskustva (0-60)'),
                                validator: _serverErrors.wrap(
                                    'yearsOfExperience',
                                    (v) => Validators.numberRange(v, 0, 60, label: 'Godine iskustva', isInt: true)),
                              ),
                            ),
                            const SizedBox(width: 16),
                            Expanded(
                              child: TextFormField(
                                controller: _price,
                                keyboardType: TextInputType.number,
                                decoration: const InputDecoration(labelText: 'Mjesečna cijena (1-1000 KM)'),
                                validator: _serverErrors.wrap(
                                    'monthlyPrice',
                                    (v) => Validators.numberRange(v, 1, 1000, label: 'Cijena')),
                              ),
                            ),
                          ]),
                          const SizedBox(height: 16),
                          Align(
                            alignment: Alignment.centerLeft,
                            child: Text('Specijalizacije', style: TextStyle(color: Colors.white.withValues(alpha: 0.9))),
                          ),
                          const SizedBox(height: 4),
                          Container(
                            padding: const EdgeInsets.all(10),
                            decoration: BoxDecoration(
                              color: AppColors.panelDark,
                              borderRadius: BorderRadius.circular(10),
                              border: _specializationIds.isEmpty
                                  ? Border.all(color: Colors.white24)
                                  : null,
                            ),
                            child: Wrap(
                              spacing: 8,
                              runSpacing: 4,
                              children: _fitnessGoals.map((goal) {
                                final id = goal['id'] as int;
                                final selected = _specializationIds.contains(id);
                                return FilterChip(
                                  label: Text(goal['name'] as String),
                                  selected: selected,
                                  onSelected: (value) => setState(() {
                                    if (value) {
                                      _specializationIds.add(id);
                                    } else {
                                      _specializationIds.remove(id);
                                    }
                                  }),
                                  selectedColor: AppColors.accent,
                                  labelStyle: TextStyle(color: selected ? Colors.black : Colors.white),
                                  backgroundColor: AppColors.panelLight,
                                );
                              }).toList(),
                            ),
                          ),
                          const SizedBox(height: 24),
                          const Text('Lozinka',
                              style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 18)),
                          const SizedBox(height: 16),
                          Row(children: [
                            Expanded(
                              child: TextFormField(
                                controller: _password,
                                obscureText: true,
                                decoration: const InputDecoration(labelText: 'Lozinka'),
                                validator: _serverErrors.wrap('password', Validators.password),
                              ),
                            ),
                            const SizedBox(width: 16),
                            Expanded(
                              child: TextFormField(
                                controller: _confirmPassword,
                                obscureText: true,
                                decoration: const InputDecoration(labelText: 'Potvrdite lozinku'),
                                validator: _serverErrors.wrap(
                                    'confirmPassword', (v) => Validators.confirmPassword(v, _password.text)),
                              ),
                            ),
                          ]),
                          const SizedBox(height: 24),
                          const Text('Certifikati struke (1-5 fajlova, pdf/jpg/png, max 5 MB)',
                              style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 18)),
                          const SizedBox(height: 12),
                          ..._certificates.map(
                            (c) => Card(
                              margin: const EdgeInsets.only(bottom: 8),
                              child: ListTile(
                                leading: const Icon(Icons.description, color: AppColors.accent),
                                title: Text(c.name),
                                subtitle: Text('${(c.sizeBytes / 1024).toStringAsFixed(0)} KB'),
                                trailing: IconButton(
                                  icon: const Icon(Icons.delete_outline, color: AppColors.danger),
                                  onPressed: () => setState(() => _certificates.remove(c)),
                                ),
                              ),
                            ),
                          ),
                          OutlinedButton.icon(
                            onPressed: _pickCertificates,
                            icon: const Icon(Icons.upload_file),
                            label: const Text('Dodaj certifikat'),
                          ),
                          const SizedBox(height: 28),
                          Row(
                            children: [
                              Expanded(
                                child: ElevatedButton(
                                  onPressed: _submitting ? null : _submit,
                                  child: _submitting
                                      ? const SizedBox(
                                          height: 20,
                                          width: 20,
                                          child: CircularProgressIndicator(strokeWidth: 2, color: Colors.black))
                                      : const Text('POŠALJI ZAHTJEV ZA REGISTRACIJU'),
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            ),
    );
  }
}

class _PickedCertificate {
  _PickedCertificate({required this.path, required this.name, required this.sizeBytes});

  final String path;
  final String name;
  final int sizeBytes;
}
