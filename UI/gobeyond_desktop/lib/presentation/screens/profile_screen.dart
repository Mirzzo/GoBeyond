import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/models/app_role.dart';
import '../../core/services/auth_service.dart';
import '../../core/services/profile_service.dart';
import '../../core/services/reference_data_service.dart';
import '../../core/session/session_controller.dart';
import '../../core/theme/app_theme.dart';
import '../../core/utils/api_error.dart';
import '../../core/utils/server_errors.dart';
import '../../core/utils/validators.dart';
import '../widgets/avatar.dart';
import '../widgets/certificate_viewer.dart';
import '../widgets/dialogs.dart';

class ProfileScreen extends StatefulWidget {
  const ProfileScreen({super.key});

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  final _profileService = ProfileService();
  final _referenceDataService = ReferenceDataService();
  final _authService = AuthService();
  final _formKey = GlobalKey<FormState>();
  final _serverErrors = ServerErrors();

  bool _loading = true;
  bool _saving = false;
  Map<String, dynamic>? _profile;
  List<Map<String, dynamic>> _genders = const [];
  List<Map<String, dynamic>> _trainingTypes = const [];
  List<Map<String, dynamic>> _fitnessGoals = const [];
  List<Map<String, dynamic>> _certificates = const [];
  bool _isMentor = false;

  final _firstName = TextEditingController();
  final _lastName = TextEditingController();
  final _username = TextEditingController();
  final _email = TextEditingController();
  final _phone = TextEditingController();
  final _nickname = TextEditingController();
  final _bio = TextEditingController();
  final _years = TextEditingController();
  final _price = TextEditingController();

  DateTime? _dateOfBirth;
  int? _genderId;
  int? _trainingTypeId;
  final Set<int> _specializationIds = {};

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    for (final c in [_firstName, _lastName, _username, _email, _phone, _nickname, _bio, _years, _price]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final profile = await _profileService.getMe();
      _isMentor = profile['mentor'] != null;
      final futures = <Future<List<Map<String, dynamic>>>>[
        _referenceDataService.list(ReferenceResource.genders),
      ];
      if (_isMentor) {
        futures.add(_referenceDataService.list(ReferenceResource.trainingTypes));
        futures.add(_referenceDataService.list(ReferenceResource.fitnessGoals));
      }
      final results = await Future.wait(futures);

      List<Map<String, dynamic>> certificates = const [];
      if (_isMentor) {
        certificates = await _profileService.myCertificates();
      }

      if (!mounted) return;
      setState(() {
        _profile = profile;
        _genders = results[0];
        if (_isMentor) {
          _trainingTypes = results[1];
          _fitnessGoals = results[2];
        }
        _certificates = certificates;
        _applyProfileToControllers(profile);
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  void _applyProfileToControllers(Map<String, dynamic> profile) {
    _firstName.text = profile['firstName'] as String? ?? '';
    _lastName.text = profile['lastName'] as String? ?? '';
    _username.text = profile['username'] as String? ?? '';
    _email.text = profile['email'] as String? ?? '';
    _phone.text = profile['phoneNumber'] as String? ?? '';
    _genderId = profile['genderId'] as int?;
    final dob = profile['dateOfBirth'] as String?;
    _dateOfBirth = dob != null ? DateTime.tryParse(dob) : null;

    final mentor = profile['mentor'] as Map<String, dynamic>?;
    if (mentor != null) {
      _trainingTypeId = mentor['trainingTypeId'] as int?;
      _nickname.text = mentor['nickname'] as String? ?? '';
      _bio.text = mentor['bio'] as String? ?? '';
      _years.text = (mentor['yearsOfExperience'] ?? '').toString();
      _price.text = (mentor['monthlyPrice'] ?? '').toString();
      _specializationIds
        ..clear()
        ..addAll(((mentor['specializationIds'] as List<dynamic>?) ?? const []).map((e) => e as int));
    }
  }

  Future<void> _pickDateOfBirth() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _dateOfBirth ?? DateTime(now.year - 25, now.month, now.day),
      firstDate: DateTime(now.year - 90),
      lastDate: DateTime(now.year - 16),
      helpText: 'Odaberite datum rođenja',
    );
    if (picked != null) setState(() => _dateOfBirth = picked);
  }

  Future<void> _save() async {
    _serverErrors.clear();
    if (!_formKey.currentState!.validate() || _dateOfBirth == null || _genderId == null) {
      setState(() {});
      if (_dateOfBirth == null || _genderId == null) {
        showErrorSnack(context, 'Popunite datum rođenja i spol.');
      }
      return;
    }
    if (_isMentor && (_trainingTypeId == null || _specializationIds.isEmpty)) {
      showErrorSnack(context, 'Odaberite vrstu treninga i barem jednu specijalizaciju.');
      return;
    }

    setState(() => _saving = true);
    try {
      final dob = _dateOfBirth!;
      final dobString =
          '${dob.year.toString().padLeft(4, '0')}-${dob.month.toString().padLeft(2, '0')}-${dob.day.toString().padLeft(2, '0')}';

      final payload = <String, dynamic>{
        'username': _username.text.trim(),
        'firstName': _firstName.text.trim(),
        'lastName': _lastName.text.trim(),
        'email': _email.text.trim(),
        'phoneNumber': _phone.text.trim().isEmpty ? null : _phone.text.trim(),
        'dateOfBirth': dobString,
        'genderId': _genderId,
        if (_isMentor)
          'mentor': {
            'trainingTypeId': _trainingTypeId,
            'nickname': _nickname.text.trim().isEmpty ? null : _nickname.text.trim(),
            'bio': _bio.text.trim(),
            'yearsOfExperience': int.tryParse(_years.text.trim()) ?? 0,
            'monthlyPrice': double.tryParse(_price.text.trim()) ?? 0,
            'specializationIds': _specializationIds.toList(),
          },
      };

      final updated = await _profileService.updateMe(payload);
      if (!mounted) return;
      setState(() {
        _profile = updated;
        _applyProfileToControllers(updated);
      });

      final session = context.read<SessionController>();
      if (session.user != null) {
        session.updateUser(session.user!.copyWith(
          firstName: _firstName.text.trim(),
          lastName: _lastName.text.trim(),
          email: _email.text.trim(),
        ));
      }

      if (mounted) showSuccessSnack(context, 'Profil je uspješno sačuvan.');
    } catch (error) {
      final apiError = ApiError.from(error, fallback: 'Čuvanje profila nije uspjelo.');
      setState(() => _serverErrors.apply(apiError.fieldErrors));
      _formKey.currentState!.validate();
      if (mounted) showErrorSnack(context, apiError.message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _uploadPhoto() async {
    final result = await FilePicker.pickFile(type: FileType.image);
    if (result == null || result.path == null) return;
    try {
      await _profileService.uploadPhoto(result.path!);
      await _load();
      if (!mounted) return;
      showSuccessSnack(context, 'Profilna slika je ažurirana.');
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _removePhoto() async {
    final confirmed = await showConfirmDialog(context,
        title: 'Ukloni profilnu sliku', message: 'Da li ste sigurni da želite ukloniti profilnu sliku?', danger: true);
    if (!confirmed) return;
    try {
      await _profileService.deletePhoto();
      await _load();
      if (!mounted) return;
      showSuccessSnack(context, 'Profilna slika je uklonjena.');
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _addCertificate() async {
    final result = await FilePicker.pickFiles(
      type: FileType.custom,
      allowedExtensions: const ['pdf', 'jpg', 'jpeg', 'png'],
    );
    if (result.isEmpty) return;
    final paths = result.where((f) => f.path != null).map((f) => f.path!).toList();
    if (paths.isEmpty) return;
    try {
      final updated = await _profileService.addCertificates(paths);
      if (!mounted) return;
      setState(() => _certificates = updated);
      showSuccessSnack(context, 'Certifikat je dodan.');
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _previewCertificate(Map<String, dynamic> certificate) async {
    final fileUrl = certificate['fileUrl'] as String?;
    if (fileUrl == null) return;
    await showGbDialog<void>(
      context: context,
      title: certificate['fileName'] as String? ?? 'Certifikat',
      width: 640,
      child: CertificateViewer(fileUrl: fileUrl, fileName: certificate['fileName'] as String? ?? ''),
      actions: [ElevatedButton(onPressed: () => Navigator.of(context).pop(), child: const Text('ZATVORI'))],
    );
  }

  Future<void> _deleteCertificate(Map<String, dynamic> certificate) async {
    if (_certificates.length <= 1) {
      showErrorSnack(context, 'Morate imati barem jedan certifikat.');
      return;
    }
    final confirmed = await showConfirmDialog(context,
        title: 'Obriši certifikat',
        message: 'Da li ste sigurni da želite obrisati certifikat "${certificate['fileName']}"?',
        danger: true);
    if (!confirmed) return;
    try {
      await _profileService.deleteCertificate(certificate['id'] as int);
      if (!mounted) return;
      setState(() => _certificates = _certificates.where((c) => c['id'] != certificate['id']).toList());
      showSuccessSnack(context, 'Certifikat je obrisan.');
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _changePassword() {
    return showDialog<void>(
      context: context,
      builder: (_) => _ChangePasswordDialog(authService: _authService),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Moj profil'), backgroundColor: AppColors.panel),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : SingleChildScrollView(
              padding: const EdgeInsets.all(24),
              child: Center(
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 760),
                  child: Column(
                    children: [
                      Container(
                        padding: const EdgeInsets.all(24),
                        decoration: BoxDecoration(color: AppColors.panel, borderRadius: BorderRadius.circular(20)),
                        child: Row(
                          children: [
                            GbAvatar(imageUrl: _profile?['profileImageUrl'] as String?, size: 96),
                            const SizedBox(width: 20),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text('${_profile?['firstName']} ${_profile?['lastName']}',
                                      style: const TextStyle(color: Colors.white, fontSize: 20, fontWeight: FontWeight.bold)),
                                  Text(
                                    _profile?['role'] != null ? AppRole.fromWire(_profile!['role']).displayName : '',
                                    style: const TextStyle(color: AppColors.textMuted),
                                  ),
                                  const SizedBox(height: 10),
                                  Wrap(spacing: 10, children: [
                                    OutlinedButton.icon(
                                        onPressed: _uploadPhoto, icon: const Icon(Icons.upload), label: const Text('Promijeni sliku')),
                                    OutlinedButton.icon(
                                        onPressed: _removePhoto, icon: const Icon(Icons.delete_outline), label: const Text('Ukloni sliku')),
                                    OutlinedButton.icon(
                                        onPressed: _changePassword, icon: const Icon(Icons.lock_outline), label: const Text('Promijeni lozinku')),
                                  ]),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 20),
                      Container(
                        padding: const EdgeInsets.all(24),
                        decoration: BoxDecoration(color: AppColors.panel, borderRadius: BorderRadius.circular(20)),
                        child: Form(
                          key: _formKey,
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              const Text('Osnovni podaci',
                                  style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 16)),
                              const SizedBox(height: 14),
                              Row(children: [
                                Expanded(
                                    child: TextFormField(
                                  controller: _firstName,
                                  decoration: const InputDecoration(labelText: 'Ime'),
                                  validator: _serverErrors.wrap('firstName', (v) => Validators.lengthRange(v, 2, 50, label: 'Ime')),
                                )),
                                const SizedBox(width: 16),
                                Expanded(
                                    child: TextFormField(
                                  controller: _lastName,
                                  decoration: const InputDecoration(labelText: 'Prezime'),
                                  validator: _serverErrors.wrap('lastName', (v) => Validators.lengthRange(v, 2, 50, label: 'Prezime')),
                                )),
                              ]),
                              const SizedBox(height: 14),
                              Row(children: [
                                Expanded(
                                    child: TextFormField(
                                  controller: _username,
                                  decoration: const InputDecoration(labelText: 'Korisničko ime'),
                                  validator: _serverErrors.wrap('username', Validators.username),
                                )),
                                const SizedBox(width: 16),
                                Expanded(
                                    child: TextFormField(
                                  controller: _email,
                                  decoration: const InputDecoration(labelText: 'Email'),
                                  validator: _serverErrors.wrap('email', Validators.email),
                                )),
                              ]),
                              const SizedBox(height: 14),
                              Row(children: [
                                Expanded(
                                    child: TextFormField(
                                  controller: _phone,
                                  decoration: const InputDecoration(labelText: 'Telefon (opciono)'),
                                  validator: _serverErrors.wrap('phoneNumber', (v) => Validators.phone(v)),
                                )),
                                const SizedBox(width: 16),
                                Expanded(
                                  child: InkWell(
                                    onTap: _pickDateOfBirth,
                                    child: InputDecorator(
                                      decoration: const InputDecoration(
                                          labelText: 'Datum rođenja', suffixIcon: Icon(Icons.calendar_today, size: 18)),
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
                              DropdownButtonFormField<int>(
                                initialValue: _genderId,
                                decoration: const InputDecoration(labelText: 'Spol'),
                                items: _genders.map((g) => DropdownMenuItem(value: g['id'] as int, child: Text(g['name'] as String))).toList(),
                                onChanged: (value) => setState(() => _genderId = value),
                              ),
                              if (_isMentor) ...[
                                const SizedBox(height: 24),
                                const Text('Mentorski podaci',
                                    style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 16)),
                                const SizedBox(height: 14),
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
                                  maxLines: 4,
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
                                    validator: _serverErrors.wrap(
                                        'yearsOfExperience', (v) => Validators.numberRange(v, 0, 60, label: 'Godine iskustva', isInt: true)),
                                  )),
                                  const SizedBox(width: 16),
                                  Expanded(
                                      child: TextFormField(
                                    controller: _price,
                                    keyboardType: TextInputType.number,
                                    decoration: const InputDecoration(labelText: 'Mjesečna cijena (1-1000 \$)'),
                                    validator: _serverErrors.wrap(
                                        'monthlyPrice', (v) => Validators.numberRange(v, 1, 1000, label: 'Cijena', maxDecimals: 2)),
                                  )),
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
                                const SizedBox(height: 20),
                                Row(
                                  children: [
                                    const Text('Certifikati', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold, fontSize: 16)),
                                    const Spacer(),
                                    TextButton.icon(onPressed: _addCertificate, icon: const Icon(Icons.add), label: const Text('Dodaj certifikat')),
                                  ],
                                ),
                                ..._certificates.map((c) => ListTile(
                                      leading: Icon(c['isVerified'] == true ? Icons.verified : Icons.description, color: AppColors.accent),
                                      title: Text(c['fileName'] as String? ?? ''),
                                      subtitle: Text(c['isVerified'] == true ? 'Verifikovan' : 'Nije verifikovan'),
                                      onTap: () => _previewCertificate(c),
                                      trailing: Row(
                                        mainAxisSize: MainAxisSize.min,
                                        children: [
                                          IconButton(
                                            icon: const Icon(Icons.visibility_outlined, color: AppColors.textMuted),
                                            tooltip: 'Prikaži',
                                            onPressed: () => _previewCertificate(c),
                                          ),
                                          IconButton(
                                            icon: const Icon(Icons.delete_outline, color: AppColors.danger),
                                            tooltip: 'Obriši',
                                            onPressed: () => _deleteCertificate(c),
                                          ),
                                        ],
                                      ),
                                    )),
                              ],
                              const SizedBox(height: 24),
                              Align(
                                alignment: Alignment.centerRight,
                                child: ElevatedButton(
                                  onPressed: _saving ? null : _save,
                                  child: _saving
                                      ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.black))
                                      : const Text('SAČUVAJ IZMJENE'),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
    );
  }
}

/// The "Promjena lozinke" form. Its own [StatefulWidget] so its
/// [TextEditingController]s are created in `initState` and disposed in
/// `dispose` — called only once the dialog route is actually removed,
/// unlike disposing them by hand right after `await showDialog(...)`
/// returns, which raced the dialog's exit transition and threw "A
/// TextEditingController was used after being disposed."
class _ChangePasswordDialog extends StatefulWidget {
  const _ChangePasswordDialog({required this.authService});

  final AuthService authService;

  @override
  State<_ChangePasswordDialog> createState() => _ChangePasswordDialogState();
}

class _ChangePasswordDialogState extends State<_ChangePasswordDialog> {
  final _formKey = GlobalKey<FormState>();
  final _serverErrors = ServerErrors();
  final _current = TextEditingController();
  final _newPassword = TextEditingController();
  final _confirm = TextEditingController();

  @override
  void dispose() {
    _current.dispose();
    _newPassword.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    try {
      final message = await widget.authService.changePassword(
        currentPassword: _current.text,
        newPassword: _newPassword.text,
        confirmPassword: _confirm.text,
      );
      if (!mounted) return;
      Navigator.of(context).pop();
      showSuccessSnack(context, message);
    } catch (error) {
      if (!mounted) return;
      final apiError = ApiError.from(error, fallback: 'Promjena lozinke nije uspjela.');
      setState(() => _serverErrors.apply(apiError.fieldErrors));
      _formKey.currentState!.validate();
      showErrorSnack(context, apiError.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    return GbDialog(
      title: 'Promjena lozinke',
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Odustani')),
        const SizedBox(width: 8),
        ElevatedButton(onPressed: _submit, child: const Text('Promijeni lozinku')),
      ],
      child: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextFormField(
              controller: _current,
              obscureText: true,
              decoration: const InputDecoration(labelText: 'Trenutna lozinka'),
              validator: _serverErrors.wrap('currentPassword', (v) => (v == null || v.isEmpty) ? 'Unesite trenutnu lozinku.' : null),
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _newPassword,
              obscureText: true,
              decoration: const InputDecoration(labelText: 'Nova lozinka'),
              validator: _serverErrors.wrap('newPassword', Validators.password),
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _confirm,
              obscureText: true,
              decoration: const InputDecoration(labelText: 'Potvrdite novu lozinku'),
              validator: _serverErrors.wrap('confirmPassword', (v) => Validators.confirmPassword(v, _newPassword.text)),
            ),
          ],
        ),
      ),
    );
  }
}
