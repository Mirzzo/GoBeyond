import 'package:flutter/material.dart';

import '../../../core/auth/auth_scope.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/validators.dart';
import '../../../data/repositories/lookup_repository.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/gobeyond_logo.dart';
import '../../widgets/primary_button.dart';
import '../home/home_screen.dart';
import 'registration_form.dart';

/// Login/registration for the Client role only (this app is client-only per
/// the project scope). Matches the dark/yellow theme; there is no dedicated
/// mockup for this screen (course rules exempt login/registration/profile
/// from requiring mockups).
class LoginRegisterScreen extends StatefulWidget {
  const LoginRegisterScreen({super.key, this.lookupRepository});

  final LookupRepository? lookupRepository;

  @override
  State<LoginRegisterScreen> createState() => _LoginRegisterScreenState();
}

class _LoginRegisterScreenState extends State<LoginRegisterScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabController =
      TabController(length: 2, vsync: this);

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppTheme.background,
      body: SafeArea(
        child: Column(
          children: [
            const SizedBox(height: 24),
            const GoBeyondLogo(fontSize: 40),
            const SizedBox(height: 8),
            const Text(
              'Poveži se sa svojim mentorom',
              style: TextStyle(color: AppTheme.textMuted, fontSize: 13),
            ),
            const SizedBox(height: 20),
            TabBar(
              controller: _tabController,
              indicatorColor: AppTheme.accent,
              labelColor: AppTheme.accent,
              unselectedLabelColor: AppTheme.textMuted,
              labelStyle: const TextStyle(fontWeight: FontWeight.w800),
              tabs: const [
                Tab(text: 'PRIJAVA'),
                Tab(text: 'REGISTRACIJA'),
              ],
            ),
            Expanded(
              child: TabBarView(
                controller: _tabController,
                children: [
                  _LoginForm(
                    onSwitchToRegister: () => _tabController.animateTo(1),
                  ),
                  RegistrationForm(lookupRepository: widget.lookupRepository),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _LoginForm extends StatefulWidget {
  const _LoginForm({required this.onSwitchToRegister});

  final VoidCallback onSwitchToRegister;

  @override
  State<_LoginForm> createState() => _LoginFormState();
}

class _LoginFormState extends State<_LoginForm> {
  final _formKey = GlobalKey<FormState>();
  final _usernameController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _obscure = true;
  bool _submitting = false;
  String? _errorMessage;

  @override
  void dispose() {
    _usernameController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    setState(() {
      _submitting = true;
      _errorMessage = null;
    });

    try {
      await AuthScope.of(context).login(
        usernameOrEmail: _usernameController.text.trim(),
        password: _passwordController.text,
      );
      if (!mounted) return;
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => const HomeScreen()),
        (route) => false,
      );
    } catch (error) {
      final apiError = ApiException.from(error);
      if (mounted) setState(() => _errorMessage = apiError.message);
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Form(
        key: _formKey,
        child: AppPanel(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'Prijava',
                style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800),
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _usernameController,
                textInputAction: TextInputAction.next,
                decoration: const InputDecoration(
                  labelText: 'Korisničko ime ili email',
                ),
                validator: (value) => Validators.required(value,
                    label: 'Korisničko ime ili email'),
              ),
              const SizedBox(height: 14),
              TextFormField(
                controller: _passwordController,
                obscureText: _obscure,
                textInputAction: TextInputAction.done,
                onFieldSubmitted: (_) => _submit(),
                decoration: InputDecoration(
                  labelText: 'Lozinka',
                  suffixIcon: IconButton(
                    icon: Icon(_obscure
                        ? Icons.visibility_off_rounded
                        : Icons.visibility_rounded),
                    onPressed: () => setState(() => _obscure = !_obscure),
                  ),
                ),
                validator: (value) =>
                    Validators.required(value, label: 'Lozinka'),
              ),
              if (_errorMessage != null) ...[
                const SizedBox(height: 12),
                Text(
                  _errorMessage!,
                  style: const TextStyle(color: AppTheme.danger, fontSize: 13),
                ),
              ],
              const SizedBox(height: 20),
              PrimaryButton(
                label: 'PRIJAVI SE',
                isLoading: _submitting,
                onPressed: _submit,
              ),
              const SizedBox(height: 14),
              TextButton(
                onPressed: widget.onSwitchToRegister,
                child: const Text('Nemate nalog? Registrujte se'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
