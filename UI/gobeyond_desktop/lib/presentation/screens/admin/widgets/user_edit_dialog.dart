import 'package:flutter/material.dart';

import '../../../../core/services/admin_service.dart';
import '../../../../core/services/reference_data_service.dart';
import '../../../../core/theme/app_theme.dart';
import '../../../../core/utils/api_error.dart';
import '../../../../core/utils/server_errors.dart';
import '../../../../core/utils/validators.dart';
import '../../../widgets/dialogs.dart';

/// Shared "UREDI" dialog for Admin > Mentori / Klijenti / Korisnici.
/// Loads the full `AdminUserDetail`, lets the admin edit base account
/// fields, change the role (api-contract.md section 5, "Dodjela i izmjena
/// korisničkih uloga"), and edit/collect the matching mentor or client
/// sub-profile. Never asks for a password (course rule: edit forms never
/// force re-entering credentials).
///
/// Returns `true` via [Navigator.pop] if the save succeeded, so the caller
/// can refresh its list.
Future<bool?> showUserEditDialog(BuildContext context, {required int userId, bool isSelf = false}) {
  return showDialog<bool>(
    context: context,
    builder: (_) => _UserEditDialog(userId: userId, isSelf: isSelf),
  );
}

class _UserEditDialog extends StatefulWidget {
  const _UserEditDialog({required this.userId, this.isSelf = false});
  final int userId;
  final bool isSelf;

  @override
  State<_UserEditDialog> createState() => _UserEditDialogState();
}

class _UserEditDialogState extends State<_UserEditDialog> {
  final _adminService = AdminService();
  final _referenceDataService = ReferenceDataService();
  final _formKey = GlobalKey<FormState>();
  final _serverErrors = ServerErrors();

  final _firstName = TextEditingController();
  final _lastName = TextEditingController();
  final _username = TextEditingController();
  final _email = TextEditingController();
  final _phone = TextEditingController();

  // mentor sub-fields
  final _nickname = TextEditingController();
  final _bio = TextEditingController();
  final _years = TextEditingController();
  final _price = TextEditingController();

  // client sub-fields
  final _weight = TextEditingController();
  final _height = TextEditingController();
  final _experienceYears = TextEditingController();
  final _goalDescription = TextEditingController();

  bool _loading = true;
  bool _saving = false;
  String? _loadError;

  DateTime? _dateOfBirth;
  int? _genderId;
  String? _role;
  int? _trainingTypeId;
  final Set<int> _specializationIds = {};
  int? _fitnessLevelId;
  int? _fitnessGoalId;
  int? _preferredTrainingTypeId;

  bool _hadMentorProfile = false;
  bool _hadClientProfile = false;

  List<Map<String, dynamic>> _roles = const [];
  List<Map<String, dynamic>> _genders = const [];
  List<Map<String, dynamic>> _trainingTypes = const [];
  List<Map<String, dynamic>> _fitnessGoals = const [];
  List<Map<String, dynamic>> _fitnessLevels = const [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    for (final c in [
      _firstName, _lastName, _username, _email, _phone, _nickname, _bio, _years, _price,
      _weight, _height, _experienceYears, _goalDescription,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final detailFuture = _adminService.getUserDetail(widget.userId);
      final referenceFuture = Future.wait([
        _referenceDataService.roles(),
        _referenceDataService.list(ReferenceResource.genders),
        _referenceDataService.list(ReferenceResource.trainingTypes),
        _referenceDataService.list(ReferenceResource.fitnessGoals),
        _referenceDataService.list(ReferenceResource.fitnessLevels),
      ]);
      final detail = await detailFuture;
      final results = await referenceFuture;
      _roles = results[0];
      _genders = results[1];
      _trainingTypes = results[2];
      _fitnessGoals = results[3];
      _fitnessLevels = results[4];

      _firstName.text = detail['firstName'] as String? ?? '';
      _lastName.text = detail['lastName'] as String? ?? '';
      _username.text = detail['username'] as String? ?? '';
      _email.text = detail['email'] as String? ?? '';
      _phone.text = detail['phoneNumber'] as String? ?? '';
      _role = detail['role'] as String?;
      _genderId = detail['genderId'] as int?;
      final dob = detail['dateOfBirth'] as String?;
      _dateOfBirth = dob != null ? DateTime.tryParse(dob) : null;

      final mentor = detail['mentor'] as Map<String, dynamic>?;
      if (mentor != null) {
        _hadMentorProfile = true;
        _trainingTypeId = mentor['trainingTypeId'] as int?;
        _nickname.text = mentor['nickname'] as String? ?? '';
        _bio.text = mentor['bio'] as String? ?? '';
        _years.text = (mentor['yearsOfExperience'] ?? '').toString();
        _price.text = (mentor['monthlyPrice'] ?? '').toString();
        _specializationIds.addAll(((mentor['specializationIds'] as List<dynamic>?) ?? const []).map((e) => e as int));
      }

      final client = detail['client'] as Map<String, dynamic>?;
      if (client != null) {
        _hadClientProfile = true;
        _weight.text = (client['weightKg'] ?? '').toString();
        _height.text = (client['heightCm'] ?? '').toString();
        _fitnessLevelId = client['fitnessLevelId'] as int?;
        _experienceYears.text = (client['trainingExperienceYears'] ?? '').toString();
        _fitnessGoalId = client['fitnessGoalId'] as int?;
        _goalDescription.text = client['goalDescription'] as String? ?? '';
        _preferredTrainingTypeId = client['preferredTrainingTypeId'] as int?;
      }

      if (!mounted) return;
      setState(() => _loading = false);
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _loadError = ApiError.from(error).message;
      });
    }
  }

  Future<void> _pickDateOfBirth() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _dateOfBirth ?? DateTime(now.year - 25, now.month, now.day),
      firstDate: DateTime(now.year - 100),
      lastDate: now,
      helpText: 'Odaberite datum rođenja',
    );
    if (picked != null) setState(() => _dateOfBirth = picked);
  }

  Future<void> _save() async {
    _serverErrors.clear();
    if (!_formKey.currentState!.validate() || _dateOfBirth == null || _genderId == null || _role == null) {
      setState(() {});
      if (_dateOfBirth == null || _genderId == null || _role == null) {
        showErrorSnack(context, 'Popunite sva obavezna polja (datum rođenja, spol, uloga).');
      }
      return;
    }
    if (_role == 'Mentor' && (_trainingTypeId == null || _specializationIds.isEmpty)) {
      showErrorSnack(context, 'Za mentora je potrebno odabrati vrstu treninga i barem jednu specijalizaciju.');
      return;
    }
    if (_role == 'Client' && (_fitnessLevelId == null || _fitnessGoalId == null)) {
      showErrorSnack(context, 'Za klijenta je potrebno odabrati nivo spreme i cilj.');
      return;
    }

    setState(() => _saving = true);
    try {
      final dob = _dateOfBirth!;
      final dobString =
          '${dob.year.toString().padLeft(4, '0')}-${dob.month.toString().padLeft(2, '0')}-${dob.day.toString().padLeft(2, '0')}';

      final payload = <String, dynamic>{
        'firstName': _firstName.text.trim(),
        'lastName': _lastName.text.trim(),
        'username': _username.text.trim(),
        'email': _email.text.trim(),
        'phoneNumber': _phone.text.trim().isEmpty ? null : _phone.text.trim(),
        'dateOfBirth': dobString,
        'genderId': _genderId,
        'role': _role,
      };
      if (_role == 'Mentor') {
        payload['mentor'] = {
          'trainingTypeId': _trainingTypeId,
          'nickname': _nickname.text.trim().isEmpty ? null : _nickname.text.trim(),
          'bio': _bio.text.trim(),
          'yearsOfExperience': int.tryParse(_years.text.trim()) ?? 0,
          'monthlyPrice': double.tryParse(_price.text.trim()) ?? 0,
          'specializationIds': _specializationIds.toList(),
        };
      } else if (_role == 'Client') {
        payload['client'] = {
          'weightKg': double.tryParse(_weight.text.trim()) ?? 0,
          'heightCm': double.tryParse(_height.text.trim()) ?? 0,
          'fitnessLevelId': _fitnessLevelId,
          'trainingExperienceYears': int.tryParse(_experienceYears.text.trim()) ?? 0,
          'fitnessGoalId': _fitnessGoalId,
          'goalDescription': _goalDescription.text.trim().isEmpty ? null : _goalDescription.text.trim(),
          'preferredTrainingTypeId': _preferredTrainingTypeId,
        };
      }

      await _adminService.updateUser(widget.userId, payload);
      if (!mounted) return;
      showSuccessSnack(context, 'Podaci korisnika ${_firstName.text.trim()} ${_lastName.text.trim()} su sačuvani.');
      Navigator.of(context).pop(true);
    } catch (error) {
      if (!mounted) return;
      final apiError = ApiError.from(error, fallback: 'Čuvanje korisnika nije uspjelo.');
      setState(() => _serverErrors.apply(apiError.fieldErrors));
      _formKey.currentState!.validate();
      showErrorSnack(context, apiError.message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return GbDialog(
      title: 'Uredi korisnika',
      width: 680,
      actions: _loading || _loadError != null
          ? null
          : [
              TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Odustani')),
              const SizedBox(width: 8),
              ElevatedButton(
                onPressed: _saving ? null : _save,
                child: _saving
                    ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.black))
                    : const Text('SAČUVAJ'),
              ),
            ],
      child: _loading
          ? const SizedBox(height: 200, child: Center(child: CircularProgressIndicator()))
          : _loadError != null
              ? SizedBox(height: 120, child: Center(child: Text(_loadError!, style: const TextStyle(color: AppColors.danger))))
              : Form(
                  key: _formKey,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Row(children: [
                        Expanded(
                          child: TextFormField(
                            controller: _firstName,
                            decoration: const InputDecoration(labelText: 'Ime'),
                            validator: _serverErrors.wrap('firstName', (v) => Validators.lengthRange(v, 2, 50, label: 'Ime')),
                          ),
                        ),
                        const SizedBox(width: 16),
                        Expanded(
                          child: TextFormField(
                            controller: _lastName,
                            decoration: const InputDecoration(labelText: 'Prezime'),
                            validator: _serverErrors.wrap('lastName', (v) => Validators.lengthRange(v, 2, 50, label: 'Prezime')),
                          ),
                        ),
                      ]),
                      const SizedBox(height: 14),
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
                      const SizedBox(height: 14),
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
                          child: InkWell(
                            onTap: _pickDateOfBirth,
                            child: InputDecorator(
                              decoration: const InputDecoration(labelText: 'Datum rođenja', suffixIcon: Icon(Icons.calendar_today, size: 18)),
                              child: Text(
                                _dateOfBirth == null
                                    ? 'Odaberite datum'
                                    : '${_dateOfBirth!.day.toString().padLeft(2, '0')}.${_dateOfBirth!.month.toString().padLeft(2, '0')}.${_dateOfBirth!.year}.',
                                style: TextStyle(color: _dateOfBirth == null ? AppColors.textMuted : Colors.white),
                              ),
                            ),
                          ),
                        ),
                      ]),
                      const SizedBox(height: 14),
                      Row(children: [
                        Expanded(
                          child: DropdownButtonFormField<int>(
                            initialValue: _genderId,
                            decoration: const InputDecoration(labelText: 'Spol'),
                            items: _genders.map((g) => DropdownMenuItem(value: g['id'] as int, child: Text(g['name'] as String))).toList(),
                            onChanged: (value) => setState(() => _genderId = value),
                          ),
                        ),
                        const SizedBox(width: 16),
                        Expanded(
                          child: Tooltip(
                            message: widget.isSelf ? 'Ne možete promijeniti vlastitu ulogu.' : '',
                            child: DropdownButtonFormField<String>(
                              initialValue: _role,
                              decoration: InputDecoration(
                                labelText: 'Uloga',
                                errorText: _serverErrors.forField('role'),
                              ),
                              items: _roles
                                  .map((r) => DropdownMenuItem(value: r['value'] as String, child: Text(r['name'] as String)))
                                  .toList(),
                              onChanged: widget.isSelf ? null : (value) => setState(() => _role = value),
                            ),
                          ),
                        ),
                      ]),
                      if (_role == 'Mentor') ..._mentorFields(),
                      if (_role == 'Client') ..._clientFields(),
                    ],
                  ),
                ),
    );
  }

  List<Widget> _mentorFields() {
    return [
      const SizedBox(height: 20),
      const Text('Mentorski podaci', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold)),
      if (!_hadMentorProfile) ...[
        const SizedBox(height: 6),
        const Text(
          'Korisnik do sada nije imao mentorski profil — potrebno je popuniti sljedeće podatke.',
          style: TextStyle(color: AppColors.textMuted, fontSize: 12),
        ),
      ],
      const SizedBox(height: 10),
      DropdownButtonFormField<int>(
        initialValue: _trainingTypeId,
        decoration: const InputDecoration(labelText: 'Vrsta treninga'),
        items: _trainingTypes.map((t) => DropdownMenuItem(value: t['id'] as int, child: Text(t['name'] as String))).toList(),
        onChanged: (value) => setState(() => _trainingTypeId = value),
      ),
      const SizedBox(height: 14),
      TextFormField(
        controller: _nickname,
        decoration: const InputDecoration(labelText: 'Nadimak / AKA (opciono)'),
        validator: _serverErrors.wrap('nickname', (v) => Validators.optionalLengthRange(v, 2, 50, label: 'Nadimak')),
      ),
      const SizedBox(height: 14),
      TextFormField(
        controller: _bio,
        maxLines: 3,
        decoration: const InputDecoration(labelText: 'Biografija (50-4000 znakova)'),
        validator: _serverErrors.wrap('bio', (v) => Validators.lengthRange(v, 50, 4000, label: 'Biografija')),
      ),
      const SizedBox(height: 14),
      Row(children: [
        Expanded(
          child: TextFormField(
            controller: _years,
            keyboardType: TextInputType.number,
            decoration: const InputDecoration(labelText: 'Godine iskustva (0-60)'),
            validator: _serverErrors.wrap('yearsOfExperience', (v) => Validators.numberRange(v, 0, 60, label: 'Godine iskustva', isInt: true)),
          ),
        ),
        const SizedBox(width: 16),
        Expanded(
          child: TextFormField(
            controller: _price,
            keyboardType: TextInputType.number,
            decoration: const InputDecoration(labelText: 'Mjesečna cijena (1-1000 KM)'),
            validator: _serverErrors.wrap('monthlyPrice', (v) => Validators.numberRange(v, 1, 1000, label: 'Cijena', maxDecimals: 2)),
          ),
        ),
      ]),
      const SizedBox(height: 14),
      Align(alignment: Alignment.centerLeft, child: Text('Specijalizacije', style: TextStyle(color: Colors.white.withValues(alpha: 0.9)))),
      const SizedBox(height: 6),
      Container(
        padding: const EdgeInsets.all(10),
        decoration: BoxDecoration(color: AppColors.panelDark, borderRadius: BorderRadius.circular(10)),
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
    ];
  }

  List<Widget> _clientFields() {
    return [
      const SizedBox(height: 20),
      const Text('Klijentski podaci', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold)),
      if (!_hadClientProfile) ...[
        const SizedBox(height: 6),
        const Text(
          'Korisnik do sada nije imao klijentski profil — potrebno je popuniti sljedeće podatke.',
          style: TextStyle(color: AppColors.textMuted, fontSize: 12),
        ),
      ],
      const SizedBox(height: 10),
      Row(children: [
        Expanded(
          child: TextFormField(
            controller: _weight,
            keyboardType: TextInputType.number,
            decoration: const InputDecoration(labelText: 'Tjelesna težina (30-300 kg)'),
            validator: _serverErrors.wrap('weightKg', (v) => Validators.numberRange(v, 30, 300, label: 'Tjelesna težina')),
          ),
        ),
        const SizedBox(width: 16),
        Expanded(
          child: TextFormField(
            controller: _height,
            keyboardType: TextInputType.number,
            decoration: const InputDecoration(labelText: 'Visina (100-250 cm)'),
            validator: _serverErrors.wrap('heightCm', (v) => Validators.numberRange(v, 100, 250, label: 'Visina')),
          ),
        ),
      ]),
      const SizedBox(height: 14),
      Row(children: [
        Expanded(
          child: DropdownButtonFormField<int>(
            initialValue: _fitnessLevelId,
            decoration: const InputDecoration(labelText: 'Nivo spreme'),
            items: _fitnessLevels.map((f) => DropdownMenuItem(value: f['id'] as int, child: Text(f['name'] as String))).toList(),
            onChanged: (value) => setState(() => _fitnessLevelId = value),
          ),
        ),
        const SizedBox(width: 16),
        Expanded(
          child: TextFormField(
            controller: _experienceYears,
            keyboardType: TextInputType.number,
            decoration: const InputDecoration(labelText: 'Iskustvo (0-60 godina)'),
            validator: _serverErrors.wrap(
                'trainingExperienceYears', (v) => Validators.numberRange(v, 0, 60, label: 'Iskustvo', isInt: true)),
          ),
        ),
      ]),
      const SizedBox(height: 14),
      DropdownButtonFormField<int>(
        initialValue: _fitnessGoalId,
        decoration: const InputDecoration(labelText: 'Cilj'),
        items: _fitnessGoals.map((f) => DropdownMenuItem(value: f['id'] as int, child: Text(f['name'] as String))).toList(),
        onChanged: (value) => setState(() => _fitnessGoalId = value),
      ),
      const SizedBox(height: 14),
      DropdownButtonFormField<int>(
        initialValue: _preferredTrainingTypeId,
        decoration: const InputDecoration(labelText: 'Preferirana vrsta treninga (opciono)'),
        items: _trainingTypes.map((t) => DropdownMenuItem(value: t['id'] as int, child: Text(t['name'] as String))).toList(),
        onChanged: (value) => setState(() => _preferredTrainingTypeId = value),
      ),
      const SizedBox(height: 14),
      TextFormField(
        controller: _goalDescription,
        maxLines: 3,
        decoration: const InputDecoration(labelText: 'Opis cilja (opciono, do 500 znakova)'),
        validator: _serverErrors.wrap('goalDescription', (v) => Validators.optionalLengthRange(v, 0, 500, label: 'Opis cilja')),
      ),
    ];
  }
}
