import 'package:flutter/material.dart';

import '../../../core/auth/auth_scope.dart';
import '../../../core/theme/app_theme.dart';
import '../../widgets/app_panel.dart';
import '../auth/login_register_screen.dart';

class ProfileScreen extends StatefulWidget {
  const ProfileScreen({super.key});

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  late final TextEditingController _nameController;
  late final TextEditingController _emailController;
  late final TextEditingController _ageController;
  late final TextEditingController _heightController;
  late final TextEditingController _weightController;
  late final TextEditingController _fitnessLevelController;
  String? _photoUrl;

  @override
  void initState() {
    super.initState();
    final profile = AuthScope.read(context).profile;
    final firstName = profile?['firstName']?.toString() ?? '';
    final lastName = profile?['lastName']?.toString() ?? '';
    final clientProfile = profile?['clientProfile'] as Map<String, dynamic>?;

    _nameController = TextEditingController(text: '$firstName $lastName'.trim());
    _emailController = TextEditingController(text: profile?['email']?.toString() ?? '');
    _ageController = TextEditingController(text: clientProfile?['age']?.toString() ?? '25');
    _heightController = TextEditingController(text: clientProfile?['height']?.toString() ?? '182');
    _weightController = TextEditingController(text: clientProfile?['weight']?.toString() ?? '78.5');
    _fitnessLevelController = TextEditingController(
      text: clientProfile?['fitnessLevel']?.toString() ?? 'Intermediate',
    );
    _photoUrl = profile?['profileImageUrl']?.toString();
  }

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _ageController.dispose();
    _heightController.dispose();
    _weightController.dispose();
    _fitnessLevelController.dispose();
    super.dispose();
  }

  Future<void> _saveProfile() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    final auth = AuthScope.read(context);
    final parts = _nameController.text.trim().split(RegExp(r'\s+'));
    final firstName = parts.isEmpty ? 'Client' : parts.first;
    final lastName = parts.length > 1 ? parts.sublist(1).join(' ') : 'User';

    final success = await auth.updateProfile({
      'firstName': firstName,
      'lastName': lastName,
      'email': _emailController.text.trim(),
      'profileImageUrl': _photoUrl,
      'clientProfile': {
        'weight': double.parse(_weightController.text.trim()),
        'height': double.parse(_heightController.text.trim()),
        'age': int.parse(_ageController.text.trim()),
        'fitnessLevel': _fitnessLevelController.text.trim(),
      },
    });

    if (!success || !mounted) {
      return;
    }

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Profile changes saved.')),
    );
  }

  Future<void> _updatePhoto() async {
    final controller = TextEditingController(text: _photoUrl ?? '');
    final photoUrl = await showDialog<String>(
      context: context,
      builder: (context) {
        return AlertDialog(
          title: const Text('Profile photo URL'),
          content: TextField(
            controller: controller,
            decoration: const InputDecoration(
              labelText: 'Photo URL',
              hintText: 'https://...',
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(context).pop(),
              child: const Text('Cancel'),
            ),
            ElevatedButton(
              onPressed: () => Navigator.of(context).pop(controller.text.trim()),
              child: const Text('Save'),
            ),
          ],
        );
      },
    );

    controller.dispose();

    if (photoUrl == null || !mounted) {
      return;
    }

    setState(() {
      _photoUrl = photoUrl.isEmpty ? null : photoUrl;
    });
  }

  Future<void> _logout() async {
    await AuthScope.read(context).logout();
    if (!mounted) {
      return;
    }

    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute<void>(builder: (_) => const LoginRegisterScreen()),
      (_) => false,
    );
  }

  @override
  Widget build(BuildContext context) {
    final auth = AuthScope.of(context);

    return SafeArea(
      child: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 18, 20, 24),
          children: [
            Text('Profile', style: Theme.of(context).textTheme.displaySmall),
            const SizedBox(height: 6),
            Text(
              'Review your client info and update the real profile data stored in the API.',
              style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                    color: AppTheme.textMutedColor,
                  ),
            ),
            const SizedBox(height: 20),
            AppPanel(
              gradient: LinearGradient(
                colors: [
                  AppTheme.secondaryColor.withValues(alpha: 0.20),
                  AppTheme.surfaceColor,
                ],
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
              ),
              child: Row(
                children: [
                  CircleAvatar(
                    radius: 34,
                    backgroundColor: AppTheme.secondaryColor.withValues(alpha: 0.18),
                    child: Text(
                      _nameController.text.trim().isEmpty
                          ? 'CL'
                          : _nameController.text
                              .trim()
                              .split(RegExp(r'\s+'))
                              .where((part) => part.isNotEmpty)
                              .map((part) => part[0])
                              .take(2)
                              .join(),
                      style: const TextStyle(
                        color: AppTheme.secondaryColor,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(_nameController.text.trim().isEmpty ? 'Client account' : _nameController.text.trim()),
                        const SizedBox(height: 6),
                        Text(_fitnessLevelController.text.trim()),
                      ],
                    ),
                  ),
                  OutlinedButton(
                    onPressed: _updatePhoto,
                    child: const Text('Photo URL'),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 18),
            TextFormField(
              controller: _nameController,
              decoration: const InputDecoration(labelText: 'Full name'),
              validator: (value) {
                if ((value ?? '').trim().length < 2) {
                  return 'Enter at least 2 characters.';
                }
                return null;
              },
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _emailController,
              decoration: const InputDecoration(labelText: 'Email'),
              keyboardType: TextInputType.emailAddress,
              validator: (value) {
                final email = value?.trim() ?? '';
                if (email.isEmpty || !email.contains('@') || !email.contains('.')) {
                  return 'Enter a valid email format.';
                }
                return null;
              },
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: TextFormField(
                    controller: _ageController,
                    decoration: const InputDecoration(labelText: 'Age'),
                    keyboardType: TextInputType.number,
                    validator: (value) {
                      final parsed = int.tryParse(value ?? '');
                      if (parsed == null || parsed < 16 || parsed > 90) {
                        return 'Use 16-90.';
                      }
                      return null;
                    },
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: TextFormField(
                    controller: _fitnessLevelController,
                    decoration: const InputDecoration(labelText: 'Fitness level'),
                    validator: (value) {
                      if ((value ?? '').trim().length < 3) {
                        return 'Use at least 3 characters.';
                      }
                      return null;
                    },
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: TextFormField(
                    controller: _heightController,
                    decoration: const InputDecoration(labelText: 'Height (cm)'),
                    keyboardType: TextInputType.number,
                    validator: (value) {
                      final parsed = double.tryParse(value ?? '');
                      if (parsed == null || parsed < 120 || parsed > 240) {
                        return 'Use 120-240 cm.';
                      }
                      return null;
                    },
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: TextFormField(
                    controller: _weightController,
                    decoration: const InputDecoration(labelText: 'Weight (kg)'),
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    validator: (value) {
                      final parsed = double.tryParse(value ?? '');
                      if (parsed == null || parsed < 35 || parsed > 250) {
                        return 'Use 35-250 kg.';
                      }
                      return null;
                    },
                  ),
                ),
              ],
            ),
            if (auth.errorMessage != null) ...[
              const SizedBox(height: 12),
              Text(
                auth.errorMessage!,
                style: const TextStyle(color: Colors.redAccent),
              ),
            ],
            const SizedBox(height: 18),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton(
                onPressed: auth.isBusy ? null : _saveProfile,
                child: auth.isBusy
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text('Save changes'),
              ),
            ),
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              child: OutlinedButton(
                onPressed: auth.isBusy ? null : _logout,
                child: const Text('Logout'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
