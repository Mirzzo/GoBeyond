import 'package:flutter/material.dart';

import '../../../core/auth/auth_scope.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/date_of_birth_range.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/utils/server_errors.dart';
import '../../../core/utils/validators.dart';
import '../../../data/models/lookup_item.dart';
import '../../../data/repositories/lookup_repository.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/lookup_dropdown.dart';
import '../../widgets/primary_button.dart';
import '../../widgets/state_views.dart';
import '../home/home_screen.dart';

/// The full client registration form (mockup-exempt) covering every field
/// `POST /api/auth/register/client` accepts, each with its own validation
/// message and server-side error mapping.
class RegistrationForm extends StatefulWidget {
  const RegistrationForm({super.key, this.lookupRepository});

  final LookupRepository? lookupRepository;

  @override
  State<RegistrationForm> createState() => _RegistrationFormState();
}

class _RegistrationFormState extends State<RegistrationForm>
    with ServerErrorsMixin<RegistrationForm> {
  late final LookupRepository _lookupRepository =
      widget.lookupRepository ?? ApiLookupRepository();

  final _formKey = GlobalKey<FormState>();

  final _firstNameController = TextEditingController();
  final _lastNameController = TextEditingController();
  final _usernameController = TextEditingController();
  final _emailController = TextEditingController();
  final _phoneController = TextEditingController();
  final _weightController = TextEditingController();
  final _heightController = TextEditingController();
  final _experienceController = TextEditingController();
  final _goalDescriptionController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();

  DateTime? _dateOfBirth;
  int? _genderId;
  int? _fitnessLevelId;
  int? _fitnessGoalId;
  int? _preferredTrainingTypeId;
  bool _obscurePassword = true;
  bool _obscureConfirm = true;
  bool _submitting = false;
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
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  Future<void> _pickDateOfBirth() async {
    final now = DateTime.now();
    // The picker only offers dates the backend accepts.
    final range = DateOfBirthRange(now);
    final picked = await showDatePicker(
      context: context,
      initialDate: DateTime(now.year - 20, now.month, now.day),
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

  Future<void> _submit() async {
    clearServerErrors();
    setState(() {
      _formError = null;
      _dobError = null;
    });

    final formValid = _formKey.currentState?.validate() ?? false;
    if (_dateOfBirth == null) {
      setState(() => _dobError = 'Datum rođenja je obavezan.');
    }
    if (!formValid || _dateOfBirth == null) return;

    setState(() => _submitting = true);
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
        'password': _passwordController.text,
        'confirmPassword': _confirmPasswordController.text,
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
      };

      await AuthScope.of(context).registerClient(payload);
      if (!mounted) return;
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => const HomeScreen()),
        (route) => false,
      );
    } catch (error) {
      if (!mounted) return;
      final apiError = ApiException.from(error);
      if (apiError.errors.isNotEmpty) {
        applyServerErrors(apiError.errors, _formKey);
        setState(() => _dobError ??= serverError('dateOfBirth'));
      }
      setState(() => _formError = apiError.message);
      // Field errors render at the top of a long form; the user is usually
      // scrolled down near REGISTRUJ SE, so they'd otherwise only see the
      // generic message below and never notice which field is wrong.
      if (_scrollController.hasClients) {
        _scrollController.animateTo(0,
            duration: const Duration(milliseconds: 250), curve: Curves.easeOut);
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<_LookupData>(
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

        final lookups = snapshot.data!;
        return SingleChildScrollView(
          controller: _scrollController,
          padding: const EdgeInsets.all(20),
          child: Form(
            key: _formKey,
            autovalidateMode: AutovalidateMode.onUserInteraction,
            child: AppPanel(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Text('Registracija (klijent)',
                      style:
                          TextStyle(fontSize: 20, fontWeight: FontWeight.w800)),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: _firstNameController,
                    decoration: const InputDecoration(labelText: 'Ime'),
                    validator: (v) =>
                        Validators.textLength(v,
                            min: 2, max: 50, label: 'Ime') ??
                        serverError('firstName'),
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _lastNameController,
                    decoration: const InputDecoration(labelText: 'Prezime'),
                    validator: (v) =>
                        Validators.textLength(v,
                            min: 2, max: 50, label: 'Prezime') ??
                        serverError('lastName'),
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _usernameController,
                    decoration: const InputDecoration(
                        labelText: 'Korisničko ime',
                        helperText:
                            '3–30 znakova: slova, brojevi, tačka, donja crta'),
                    validator: (v) =>
                        Validators.username(v) ?? serverError('username'),
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _emailController,
                    keyboardType: TextInputType.emailAddress,
                    decoration: const InputDecoration(labelText: 'Email'),
                    validator: (v) =>
                        Validators.email(v) ?? serverError('email'),
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _phoneController,
                    keyboardType: TextInputType.phone,
                    decoration: const InputDecoration(
                        labelText: 'Broj telefona (opciono)',
                        helperText: 'npr. +387 61 123 456'),
                    validator: (v) =>
                        Validators.optionalPhone(v) ??
                        serverError('phoneNumber'),
                  ),
                  const SizedBox(height: 12),
                  _DatePickerField(
                    label: 'Datum rođenja',
                    value: _dateOfBirth,
                    onTap: _pickDateOfBirth,
                    errorText: _dobError,
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
                        const TextInputType.numberWithOptions(decimal: true),
                    decoration: const InputDecoration(
                        labelText: 'Tjelesna težina (kg)'),
                    validator: (v) =>
                        Validators.numberRange(v,
                            min: 30,
                            max: 300,
                            label: 'Težina',
                            gender: LabelGender.feminine) ??
                        serverError('weightKg'),
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _heightController,
                    keyboardType:
                        const TextInputType.numberWithOptions(decimal: true),
                    decoration: const InputDecoration(labelText: 'Visina (cm)'),
                    validator: (v) =>
                        Validators.numberRange(v,
                            min: 100,
                            max: 250,
                            label: 'Visina',
                            gender: LabelGender.feminine) ??
                        serverError('heightCm'),
                  ),
                  const SizedBox(height: 12),
                  LookupDropdown(
                    label: 'Nivo fizičke spreme',
                    items: lookups.fitnessLevels,
                    value: _fitnessLevelId,
                    onChanged: (v) => setState(() => _fitnessLevelId = v),
                    errorText: serverError('fitnessLevelId'),
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
                            min: 0, max: 60, label: 'Iskustvo', isInt: true) ??
                        serverError('trainingExperienceYears'),
                  ),
                  const SizedBox(height: 12),
                  LookupDropdown(
                    label: 'Fitness cilj',
                    items: lookups.fitnessGoals,
                    value: _fitnessGoalId,
                    onChanged: (v) => setState(() => _fitnessGoalId = v),
                    errorText: serverError('fitnessGoalId'),
                    requiredMessage: 'Odaberite fitness cilj.',
                  ),
                  const SizedBox(height: 12),
                  LookupDropdown(
                    label: 'Željena vrsta treninga (opciono)',
                    items: lookups.trainingTypes,
                    value: _preferredTrainingTypeId,
                    onChanged: (v) =>
                        setState(() => _preferredTrainingTypeId = v),
                    allowEmpty: true,
                    errorText: serverError('preferredTrainingTypeId'),
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
                        serverError('goalDescription'),
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _passwordController,
                    obscureText: _obscurePassword,
                    decoration: InputDecoration(
                      labelText: 'Lozinka',
                      helperText: '8–64 znaka, barem jedno slovo i jedan broj',
                      suffixIcon: IconButton(
                        icon: Icon(_obscurePassword
                            ? Icons.visibility_off_rounded
                            : Icons.visibility_rounded),
                        onPressed: () => setState(
                            () => _obscurePassword = !_obscurePassword),
                      ),
                    ),
                    validator: (v) =>
                        Validators.password(v) ?? serverError('password'),
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _confirmPasswordController,
                    obscureText: _obscureConfirm,
                    decoration: InputDecoration(
                      labelText: 'Potvrdi lozinku',
                      suffixIcon: IconButton(
                        icon: Icon(_obscureConfirm
                            ? Icons.visibility_off_rounded
                            : Icons.visibility_rounded),
                        onPressed: () =>
                            setState(() => _obscureConfirm = !_obscureConfirm),
                      ),
                    ),
                    validator: (v) =>
                        Validators.confirmPassword(
                            v, _passwordController.text) ??
                        serverError('confirmPassword'),
                  ),
                  if (_formError != null) ...[
                    const SizedBox(height: 12),
                    Text(_formError!,
                        style: const TextStyle(
                            color: AppTheme.danger, fontSize: 13)),
                  ],
                  const SizedBox(height: 20),
                  PrimaryButton(
                    label: 'REGISTRUJ SE',
                    isLoading: _submitting,
                    onPressed: _submit,
                  ),
                ],
              ),
            ),
          ),
        );
      },
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

class _DatePickerField extends StatelessWidget {
  const _DatePickerField({
    required this.label,
    required this.value,
    required this.onTap,
    this.errorText,
  });

  final String label;
  final DateTime? value;
  final VoidCallback onTap;
  final String? errorText;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(18),
      child: InputDecorator(
        decoration: InputDecoration(
          labelText: label,
          errorText: errorText,
          suffixIcon: const Icon(Icons.calendar_month_rounded),
        ),
        child: Text(
          value == null ? 'Odaberite datum' : Formatters.dateOnly(value!),
          style: TextStyle(
            color: value == null ? AppTheme.textMuted : AppTheme.textPrimary,
          ),
        ),
      ),
    );
  }
}
