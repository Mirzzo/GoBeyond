import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/auth/auth_scope.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/date_of_birth_range.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/utils/server_errors.dart';
import '../../../core/utils/validators.dart';
import '../../../data/models/lookup_item.dart';
import '../../../data/models/user_profile.dart';
import '../../../data/repositories/lookup_repository.dart';
import '../../../data/repositories/profile_repository.dart';
import '../../widgets/app_dialogs.dart';
import '../../widgets/app_network_image.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/gb_scaffold.dart';
import '../../widgets/lookup_dropdown.dart';
import '../../widgets/primary_button.dart';
import '../../widgets/state_views.dart';
import '../progress/training_history_screen.dart';

/// Moj profil: view/edit every registration field, photo upload/remove, own
/// password change (requires current password), and the entry point to
/// Historija treninga.
class ProfileScreen extends StatefulWidget {
  const ProfileScreen(
      {super.key, this.lookupRepository, this.profileRepository});

  final LookupRepository? lookupRepository;
  final ProfileRepository? profileRepository;

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen>
    with ServerErrorsMixin<ProfileScreen> {
  late final LookupRepository _lookupRepository =
      widget.lookupRepository ?? ApiLookupRepository();
  late final ProfileRepository _profileRepository =
      widget.profileRepository ?? ApiProfileRepository();

  final _formKey = GlobalKey<FormState>();
  final _picker = ImagePicker();

  final _firstNameController = TextEditingController();
  final _lastNameController = TextEditingController();
  final _usernameController = TextEditingController();
  final _emailController = TextEditingController();
  final _phoneController = TextEditingController();
  final _weightController = TextEditingController();
  final _heightController = TextEditingController();
  final _experienceController = TextEditingController();
  final _goalDescriptionController = TextEditingController();

  DateTime? _dateOfBirth;
  int? _genderId;
  int? _fitnessLevelId;
  int? _fitnessGoalId;
  int? _preferredTrainingTypeId;

  bool _initialized = false;
  bool _saving = false;
  bool _uploadingPhoto = false;
  String? _formError;
  String? _dobError;
  final _scrollController = ScrollController();

  late Future<_LookupData> _lookupFuture;

  @override
  void initState() {
    super.initState();
    _lookupFuture = _loadLookups();
  }

  Future<_LookupData> _loadLookups() async {
    final results = await Future.wait([
      _lookupRepository.getGenders(),
      _lookupRepository.getFitnessLevels(),
      _lookupRepository.getFitnessGoals(),
      _lookupRepository.getTrainingTypes(),
    ]);
    return _LookupData(
      genders: results[0],
      fitnessLevels: results[1],
      fitnessGoals: results[2],
      trainingTypes: results[3],
    );
  }

  void _initFromProfile(UserProfile profile) {
    if (_initialized) return;
    _initialized = true;
    _firstNameController.text = profile.firstName;
    _lastNameController.text = profile.lastName;
    _usernameController.text = profile.username;
    _emailController.text = profile.email;
    _phoneController.text = profile.phoneNumber ?? '';
    _dateOfBirth = Formatters.tryParseIso(profile.dateOfBirth) ??
        DateTime.tryParse(profile.dateOfBirth);
    _genderId = profile.genderId;
    final client = profile.client;
    if (client != null) {
      _weightController.text = client.weightKg.toString();
      _heightController.text = client.heightCm.toString();
      _experienceController.text = client.trainingExperienceYears.toString();
      _goalDescriptionController.text = client.goalDescription ?? '';
      _fitnessLevelId = client.fitnessLevelId;
      _fitnessGoalId = client.fitnessGoalId;
      _preferredTrainingTypeId = client.preferredTrainingTypeId;
    }
  }

  @override
  void dispose() {
    _firstNameController.dispose();
    _lastNameController.dispose();
    _usernameController.dispose();
    _emailController.dispose();
    _phoneController.dispose();
    _weightController.dispose();
    _heightController.dispose();
    _experienceController.dispose();
    _goalDescriptionController.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  Future<void> _pickDateOfBirth() async {
    final now = DateTime.now();
    // The picker only offers dates the backend accepts; a saved date that
    // has since fallen outside that range opens at the nearest valid date.
    final range = DateOfBirthRange(now);
    final picked = await showDatePicker(
      context: context,
      initialDate: range
          .clamp(_dateOfBirth ?? DateTime(now.year - 20, now.month, now.day)),
      firstDate: range.first,
      lastDate: range.last,
      helpText: 'Odaberite datum rođenja',
    );
    if (picked != null && mounted) {
      setState(() {
        _dateOfBirth = picked;
        _dobError = null;
      });
    }
  }

  Future<void> _changePhoto() async {
    final file =
        await _picker.pickImage(source: ImageSource.gallery, imageQuality: 85);
    if (file == null) return;

    setState(() => _uploadingPhoto = true);
    try {
      final bytes = await file.readAsBytes();
      final url = await _profileRepository.uploadPhoto(bytes, file.name);
      if (!mounted) return;
      AuthScope.of(context).setProfileImage(url);
      showSuccessSnackBar(context, 'Profilna slika je uspješno ažurirana.');
    } catch (error) {
      if (!mounted) return;
      showErrorSnackBar(context, ApiException.from(error).message);
    } finally {
      if (mounted) setState(() => _uploadingPhoto = false);
    }
  }

  Future<void> _removePhoto() async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Ukloni sliku',
      message: 'Da li ste sigurni da želite ukloniti profilnu sliku?',
      confirmLabel: 'Ukloni',
      destructive: true,
    );
    if (!confirmed) return;

    try {
      await _profileRepository.deletePhoto();
      if (!mounted) return;
      AuthScope.of(context).setProfileImage(null);
      showSuccessSnackBar(context, 'Profilna slika je uklonjena.');
    } catch (error) {
      if (!mounted) return;
      showErrorSnackBar(context, ApiException.from(error).message);
    }
  }

  Future<void> _save() async {
    clearServerErrors();
    setState(() {
      _formError = null;
      _dobError = null;
    });

    final valid = _formKey.currentState?.validate() ?? false;
    if (_dateOfBirth == null) {
      setState(() => _dobError = 'Datum rođenja je obavezan.');
    }
    if (!valid || _dateOfBirth == null) return;

    setState(() => _saving = true);
    try {
      final payload = <String, dynamic>{
        'firstName': _firstNameController.text.trim(),
        'lastName': _lastNameController.text.trim(),
        'username': _usernameController.text.trim(),
        'email': _emailController.text.trim(),
        if (_phoneController.text.trim().isNotEmpty)
          'phoneNumber': _phoneController.text.trim(),
        'dateOfBirth': Formatters.dateForApi(_dateOfBirth!),
        'genderId': _genderId,
        'client': {
          'weightKg': num.tryParse(_weightController.text.trim()),
          'heightCm': num.tryParse(_heightController.text.trim()),
          'fitnessLevelId': _fitnessLevelId,
          'trainingExperienceYears':
              int.tryParse(_experienceController.text.trim()),
          'fitnessGoalId': _fitnessGoalId,
          if (_goalDescriptionController.text.trim().isNotEmpty)
            'goalDescription': _goalDescriptionController.text.trim(),
          if (_preferredTrainingTypeId != null)
            'preferredTrainingTypeId': _preferredTrainingTypeId,
        },
      };

      await AuthScope.of(context).updateProfile(payload);
      if (!mounted) return;
      showSuccessSnackBar(context, 'Profil je uspješno ažuriran.');
    } catch (error) {
      if (!mounted) return;
      final apiError = ApiException.from(error);
      if (apiError.errors.isNotEmpty) {
        applyServerErrors(apiError.errors, _formKey);
        setState(() => _dobError ??= serverError('dateOfBirth'));
      }
      setState(() => _formError = apiError.message);
      // Field errors render near the top of a long form; the user is
      // usually scrolled down near SPREMI IZMJENE, so they'd otherwise only
      // see the generic message below and never notice which field is wrong.
      if (_scrollController.hasClients) {
        _scrollController.animateTo(0,
            duration: const Duration(milliseconds: 250), curve: Curves.easeOut);
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _openChangePassword() async {
    await showDialog<void>(
      context: context,
      builder: (_) => const _ChangePasswordDialog(),
    );
  }

  @override
  Widget build(BuildContext context) {
    final auth = AuthScope.of(context);
    final profile = auth.profile;

    return GbScaffold(
      body: profile == null
          ? (auth.profileLoadError != null
              ? ErrorView(
                  message: auth.profileLoadError!,
                  onRetry: auth.retryLoadProfile,
                )
              : const LoadingView())
          : FutureBuilder<_LookupData>(
              future: _lookupFuture,
              builder: (context, snapshot) {
                if (snapshot.connectionState != ConnectionState.done) {
                  return const LoadingView();
                }
                if (snapshot.hasError) {
                  return ErrorView(
                    message: ApiException.from(snapshot.error!).message,
                    onRetry: () => setState(() {
                      _lookupFuture = _loadLookups();
                    }),
                  );
                }
                _initFromProfile(profile);
                final lookups = snapshot.data!;

                return ListView(
                  controller: _scrollController,
                  padding: const EdgeInsets.all(20),
                  children: [
                    Center(
                      child: Stack(
                        children: [
                          AppNetworkImage(
                            url: profile.profileImageUrl,
                            width: 120,
                            height: 120,
                            borderRadius: 60,
                            yellowBorder: true,
                          ),
                          if (_uploadingPhoto)
                            const Positioned.fill(
                              child: Center(child: CircularProgressIndicator()),
                            ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 14),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        TextButton.icon(
                          onPressed: _uploadingPhoto ? null : _changePhoto,
                          icon: const Icon(Icons.photo_camera_rounded),
                          label: const Text('Promijeni sliku'),
                        ),
                        if (profile.profileImageUrl != null &&
                            profile.profileImageUrl!.isNotEmpty)
                          TextButton.icon(
                            onPressed: _removePhoto,
                            icon: const Icon(Icons.delete_outline_rounded,
                                color: AppTheme.danger),
                            label: const Text('Ukloni',
                                style: TextStyle(color: AppTheme.danger)),
                          ),
                      ],
                    ),
                    const SizedBox(height: 16),
                    Form(
                      key: _formKey,
                      autovalidateMode: AutovalidateMode.onUserInteraction,
                      child: AppPanel(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            const Text('Moji podaci',
                                style: TextStyle(
                                    fontSize: 18, fontWeight: FontWeight.w800)),
                            const SizedBox(height: 14),
                            TextFormField(
                              controller: _firstNameController,
                              decoration:
                                  const InputDecoration(labelText: 'Ime'),
                              validator: (v) =>
                                  Validators.textLength(v,
                                      min: 2, max: 50, label: 'Ime') ??
                                  serverError('firstName'),
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              controller: _lastNameController,
                              decoration:
                                  const InputDecoration(labelText: 'Prezime'),
                              validator: (v) =>
                                  Validators.textLength(v,
                                      min: 2, max: 50, label: 'Prezime') ??
                                  serverError('lastName'),
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              controller: _usernameController,
                              decoration: const InputDecoration(
                                  labelText: 'Korisničko ime'),
                              validator: (v) =>
                                  Validators.username(v) ??
                                  serverError('username'),
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              controller: _emailController,
                              keyboardType: TextInputType.emailAddress,
                              decoration:
                                  const InputDecoration(labelText: 'Email'),
                              validator: (v) =>
                                  Validators.email(v) ?? serverError('email'),
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              controller: _phoneController,
                              keyboardType: TextInputType.phone,
                              decoration: const InputDecoration(
                                  labelText: 'Broj telefona (opciono)'),
                              validator: (v) =>
                                  Validators.optionalPhone(v) ??
                                  serverError('phoneNumber'),
                            ),
                            const SizedBox(height: 12),
                            InkWell(
                              onTap: _pickDateOfBirth,
                              borderRadius: BorderRadius.circular(18),
                              child: InputDecorator(
                                decoration: InputDecoration(
                                  labelText: 'Datum rođenja',
                                  errorText: _dobError,
                                  suffixIcon:
                                      const Icon(Icons.calendar_month_rounded),
                                ),
                                child: Text(_dateOfBirth == null
                                    ? 'Odaberite datum'
                                    : Formatters.dateOnly(_dateOfBirth!)),
                              ),
                            ),
                            const SizedBox(height: 12),
                            LookupDropdown(
                              label: 'Spol',
                              items: lookups.genders,
                              value: _genderId,
                              onChanged: (v) => setState(() => _genderId = v),
                              errorText: serverError('genderId'),
                              requiredMessage: 'Odaberite spol.',
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              controller: _weightController,
                              keyboardType:
                                  const TextInputType.numberWithOptions(
                                      decimal: true),
                              decoration: const InputDecoration(
                                  labelText: 'Tjelesna težina (kg)'),
                              validator: (v) =>
                                  Validators.numberRange(v,
                                      min: 30,
                                      max: 300,
                                      label: 'Težina',
                                      gender: LabelGender.feminine) ??
                                  serverError('client.weightKg'),
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              controller: _heightController,
                              keyboardType:
                                  const TextInputType.numberWithOptions(
                                      decimal: true),
                              decoration: const InputDecoration(
                                  labelText: 'Visina (cm)'),
                              validator: (v) =>
                                  Validators.numberRange(v,
                                      min: 100,
                                      max: 250,
                                      label: 'Visina',
                                      gender: LabelGender.feminine) ??
                                  serverError('client.heightCm'),
                            ),
                            const SizedBox(height: 12),
                            LookupDropdown(
                              label: 'Nivo fizičke spreme',
                              items: lookups.fitnessLevels,
                              value: _fitnessLevelId,
                              onChanged: (v) =>
                                  setState(() => _fitnessLevelId = v),
                              errorText: serverError('client.fitnessLevelId'),
                              requiredMessage: 'Odaberite nivo fizičke spreme.',
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              controller: _experienceController,
                              keyboardType: TextInputType.number,
                              decoration: const InputDecoration(
                                  labelText: 'Godine iskustva s treniranjem'),
                              validator: (v) =>
                                  Validators.numberRange(v,
                                      min: 0,
                                      max: 60,
                                      label: 'Iskustvo',
                                      isInt: true) ??
                                  serverError('client.trainingExperienceYears'),
                            ),
                            const SizedBox(height: 12),
                            LookupDropdown(
                              label: 'Fitness cilj',
                              items: lookups.fitnessGoals,
                              value: _fitnessGoalId,
                              onChanged: (v) =>
                                  setState(() => _fitnessGoalId = v),
                              errorText: serverError('client.fitnessGoalId'),
                              requiredMessage: 'Odaberite fitness cilj.',
                            ),
                            const SizedBox(height: 12),
                            LookupDropdown(
                              label: 'Željena vrsta treninga (opciono)',
                              items: lookups.trainingTypes,
                              value: _preferredTrainingTypeId,
                              allowEmpty: true,
                              onChanged: (v) =>
                                  setState(() => _preferredTrainingTypeId = v),
                              errorText:
                                  serverError('client.preferredTrainingTypeId'),
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              controller: _goalDescriptionController,
                              maxLines: 3,
                              maxLength: 500,
                              decoration: const InputDecoration(
                                  labelText: 'Opis cilja (opciono)'),
                              validator: (v) =>
                                  Validators.textLength(v,
                                      min: 0,
                                      max: 500,
                                      label: 'Opis cilja',
                                      optional: true,
                                      gender: LabelGender.masculine) ??
                                  serverError('client.goalDescription'),
                            ),
                            if (_formError != null) ...[
                              const SizedBox(height: 12),
                              Text(_formError!,
                                  style:
                                      const TextStyle(color: AppTheme.danger)),
                            ],
                            const SizedBox(height: 18),
                            PrimaryButton(
                              label: 'SPREMI IZMJENE',
                              isLoading: _saving,
                              onPressed: _save,
                            ),
                          ],
                        ),
                      ),
                    ),
                    const SizedBox(height: 16),
                    OutlinedButton.icon(
                      onPressed: _openChangePassword,
                      icon: const Icon(Icons.lock_outline_rounded),
                      label: const SizedBox(
                        width: double.infinity,
                        child: Text('Promijeni lozinku',
                            textAlign: TextAlign.center),
                      ),
                    ),
                    const SizedBox(height: 14),
                    PrimaryButton(
                      label: 'HISTORIJA TRENINGA',
                      onPressed: () => Navigator.of(context).push(
                        MaterialPageRoute(
                            builder: (_) => const TrainingHistoryScreen()),
                      ),
                    ),
                  ],
                );
              },
            ),
    );
  }
}

class _LookupData {
  const _LookupData({
    required this.genders,
    required this.fitnessLevels,
    required this.fitnessGoals,
    required this.trainingTypes,
  });

  final List<LookupItem> genders;
  final List<LookupItem> fitnessLevels;
  final List<LookupItem> fitnessGoals;
  final List<LookupItem> trainingTypes;
}

/// Own password change: requires the current password + new + confirm,
/// matching the course rule that a self-service password change must verify
/// the old one (unlike an admin resetting someone else's).
class _ChangePasswordDialog extends StatefulWidget {
  const _ChangePasswordDialog();

  @override
  State<_ChangePasswordDialog> createState() => _ChangePasswordDialogState();
}

class _ChangePasswordDialogState extends State<_ChangePasswordDialog> {
  final _formKey = GlobalKey<FormState>();
  final _currentController = TextEditingController();
  final _newController = TextEditingController();
  final _confirmController = TextEditingController();
  bool _submitting = false;
  String? _error;

  @override
  void dispose() {
    _currentController.dispose();
    _newController.dispose();
    _confirmController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      await AuthScope.of(context).changePassword(
        currentPassword: _currentController.text,
        newPassword: _newController.text,
        confirmPassword: _confirmController.text,
      );
      if (!mounted) return;
      Navigator.of(context).pop();
      showSuccessSnackBar(context, 'Lozinka je uspješno promijenjena.');
    } catch (error) {
      if (!mounted) return;
      setState(() => _error = ApiException.from(error).message);
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Row(
        children: [
          const Expanded(child: Text('Promijeni lozinku')),
          IconButton(
            icon: const Icon(Icons.close_rounded),
            onPressed: () => Navigator.of(context).pop(),
          ),
        ],
      ),
      content: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            TextFormField(
              controller: _currentController,
              obscureText: true,
              decoration: const InputDecoration(labelText: 'Trenutna lozinka'),
              validator: (v) => Validators.required(v,
                  label: 'Trenutna lozinka', gender: LabelGender.feminine),
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _newController,
              obscureText: true,
              decoration: const InputDecoration(labelText: 'Nova lozinka'),
              validator: Validators.password,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _confirmController,
              obscureText: true,
              decoration:
                  const InputDecoration(labelText: 'Potvrdi novu lozinku'),
              validator: (v) =>
                  Validators.confirmPassword(v, _newController.text),
            ),
            if (_error != null) ...[
              const SizedBox(height: 12),
              Text(_error!,
                  style: const TextStyle(color: AppTheme.danger, fontSize: 13)),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Odustani'),
        ),
        FilledButton(
          onPressed: _submitting ? null : _submit,
          child: const Text('PROMIJENI'),
        ),
      ],
    );
  }
}
