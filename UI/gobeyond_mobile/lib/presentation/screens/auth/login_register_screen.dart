import 'package:flutter/material.dart';

import '../../../core/auth/auth_scope.dart';
import '../../../core/theme/app_theme.dart';
import '../../widgets/app_panel.dart';
import '../common/client_shell_screen.dart';

class LoginRegisterScreen extends StatefulWidget {
  const LoginRegisterScreen({super.key});

  @override
  State<LoginRegisterScreen> createState() => _LoginRegisterScreenState();
}

class _LoginRegisterScreenState extends State<LoginRegisterScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  bool _isLogin = true;
  late final TextEditingController _nameController;
  late final TextEditingController _emailController;
  late final TextEditingController _passwordController;
  late final TextEditingController _ageController;
  late final TextEditingController _heightController;
  late final TextEditingController _weightController;
  late final TextEditingController _fitnessLevelController;

  @override
  void initState() {
    super.initState();
    _nameController = TextEditingController(text: 'Luka Client');
    _emailController = TextEditingController(text: 'client@gobeyond.local');
    _passwordController = TextEditingController(text: 'Client123!');
    _ageController = TextEditingController(text: '25');
    _heightController = TextEditingController(text: '182');
    _weightController = TextEditingController(text: '78.5');
    _fitnessLevelController = TextEditingController(text: 'Intermediate');
  }

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    _ageController.dispose();
    _heightController.dispose();
    _weightController.dispose();
    _fitnessLevelController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    final auth = AuthScope.read(context);
    final success = _isLogin
        ? await auth.login(
            email: _emailController.text.trim(),
            password: _passwordController.text,
          )
        : await auth.registerClient(_buildRegistrationPayload());

    if (!success || !mounted) {
      return;
    }

    Navigator.of(context).pushReplacement(
      MaterialPageRoute<void>(
        builder: (_) => const ClientShellScreen(),
      ),
    );
  }

  Map<String, dynamic> _buildRegistrationPayload() {
    final parts = _nameController.text.trim().split(RegExp(r'\s+'));
    final firstName = parts.isEmpty ? 'Client' : parts.first;
    final lastName = parts.length > 1 ? parts.sublist(1).join(' ') : 'User';

    return {
      'firstName': firstName,
      'lastName': lastName,
      'email': _emailController.text.trim(),
      'password': _passwordController.text,
      'weight': double.parse(_weightController.text.trim()),
      'height': double.parse(_heightController.text.trim()),
      'age': int.parse(_ageController.text.trim()),
      'fitnessLevel': _fitnessLevelController.text.trim(),
    };
  }

  @override
  Widget build(BuildContext context) {
    final auth = AuthScope.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Login / Register')),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 8, 20, 24),
          children: [
            AppPanel(
              gradient: LinearGradient(
                colors: [
                  AppTheme.accentColor.withValues(alpha: 0.24),
                  AppTheme.surfaceColor,
                ],
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    _isLogin ? 'Welcome back' : 'Create client account',
                    style: Theme.of(context).textTheme.headlineMedium,
                  ),
                  const SizedBox(height: 10),
                  Text(
                    _isLogin
                        ? 'Use the seeded client credentials or your registered account.'
                        : 'Registration writes a real client profile to the GoBeyond API.',
                  ),
                ],
              ),
            ),
            const SizedBox(height: 18),
            AppPanel(
              color: AppTheme.surfaceColor,
              padding: const EdgeInsets.all(8),
              child: Row(
                children: [
                  Expanded(
                    child: _ModeButton(
                      label: 'Login',
                      selected: _isLogin,
                      onTap: () => setState(() => _isLogin = true),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: _ModeButton(
                      label: 'Register',
                      selected: !_isLogin,
                      onTap: () => setState(() => _isLogin = false),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 18),
            if (!_isLogin) ...[
              TextFormField(
                controller: _nameController,
                decoration: const InputDecoration(labelText: 'Full name'),
                validator: (value) {
                  if (_isLogin) {
                    return null;
                  }

                  if ((value ?? '').trim().length < 2) {
                    return 'Enter at least 2 characters.';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 12),
            ],
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
            TextFormField(
              controller: _passwordController,
              decoration: const InputDecoration(labelText: 'Password'),
              obscureText: true,
              validator: (value) {
                if ((value ?? '').length < 8) {
                  return 'Use at least 8 characters.';
                }
                return null;
              },
            ),
            if (!_isLogin) ...[
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: TextFormField(
                      controller: _ageController,
                      keyboardType: TextInputType.number,
                      decoration: const InputDecoration(labelText: 'Age'),
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
                      keyboardType: TextInputType.number,
                      decoration: const InputDecoration(labelText: 'Height (cm)'),
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
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                      decoration: const InputDecoration(labelText: 'Weight (kg)'),
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
            ],
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
                onPressed: auth.isBusy ? null : _submit,
                child: auth.isBusy
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : Text(_isLogin ? 'Login' : 'Create account'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ModeButton extends StatelessWidget {
  const _ModeButton({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 180),
        padding: const EdgeInsets.symmetric(vertical: 14),
        decoration: BoxDecoration(
          color: selected ? AppTheme.accentColor : Colors.transparent,
          borderRadius: BorderRadius.circular(16),
        ),
        child: Center(
          child: Text(
            label,
            style: Theme.of(context).textTheme.labelLarge?.copyWith(
                  color: selected ? const Color(0xFF16100B) : Colors.white,
                ),
          ),
        ),
      ),
    );
  }
}
