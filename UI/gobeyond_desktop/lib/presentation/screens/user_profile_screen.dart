import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/models/app_role.dart';
import '../../core/models/user_profile.dart';
import '../../core/network/api_client.dart';
import '../../core/services/user_profile_api_service.dart';
import '../../core/session/session_controller.dart';

class UserProfileScreen extends StatefulWidget {
  const UserProfileScreen({super.key});

  @override
  State<UserProfileScreen> createState() => _UserProfileScreenState();
}

class _UserProfileScreenState extends State<UserProfileScreen> {
  final _formKey = GlobalKey<FormState>();
  final _firstNameController = TextEditingController();
  final _lastNameController = TextEditingController();
  final _emailController = TextEditingController();
  final _profileImageUrlController = TextEditingController();

  final _mentorBioController = TextEditingController();
  final _mentorAgeController = TextEditingController();
  final _mentorPriceController = TextEditingController();
  String _mentorCategory = 'Hybrid';

  final _clientWeightController = TextEditingController();
  final _clientHeightController = TextEditingController();
  final _clientAgeController = TextEditingController();
  final _clientFitnessLevelController = TextEditingController();

  final _profileApiService = UserProfileApiService(ApiClient());

  bool _isLoading = false;
  bool _isSaving = false;
  String? _errorMessage;
  String? _infoMessage;

  @override
  void initState() {
    super.initState();
    _seedFromSession();
    _loadProfile();
  }

  @override
  void dispose() {
    _firstNameController.dispose();
    _lastNameController.dispose();
    _emailController.dispose();
    _profileImageUrlController.dispose();
    _mentorBioController.dispose();
    _mentorAgeController.dispose();
    _mentorPriceController.dispose();
    _clientWeightController.dispose();
    _clientHeightController.dispose();
    _clientAgeController.dispose();
    _clientFitnessLevelController.dispose();
    super.dispose();
  }

  AppRole _currentRole() {
    return context.read<SessionController>().user?.role ?? AppRole.client;
  }

  void _seedFromSession() {
    final user = context.read<SessionController>().user;
    if (user == null) {
      return;
    }

    _emailController.text = user.email;
    final parts = user.name.trim().split(RegExp(r'\s+')).where((part) => part.isNotEmpty).toList();
    if (parts.isEmpty) {
      return;
    }

    _firstNameController.text = parts.first;
    _lastNameController.text = parts.length > 1 ? parts.sublist(1).join(' ') : '';
  }

  Future<void> _loadProfile() async {
    final session = context.read<SessionController>();
    final token = session.accessToken;
    if (token == null) {
      return;
    }

    setState(() {
      _isLoading = true;
      _errorMessage = null;
      _infoMessage = null;
    });

    try {
      final profile = await _profileApiService.getProfile(accessToken: token);
      _applyProfile(profile);
      await session.syncUserFromProfile(profile);
      if (!mounted) {
        return;
      }

      setState(() {
        _infoMessage = 'Profile loaded.';
      });
    } catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _errorMessage = _extractErrorMessage(error, fallback: 'Failed to load profile.');
      });
    } finally {
      if (mounted) {
        setState(() {
          _isLoading = false;
        });
      }
    }
  }

  void _applyProfile(UserProfile profile) {
    _firstNameController.text = profile.firstName;
    _lastNameController.text = profile.lastName;
    _emailController.text = profile.email;
    _profileImageUrlController.text = profile.profileImageUrl ?? '';

    if (profile.mentorProfile != null) {
      final mentor = profile.mentorProfile!;
      _mentorBioController.text = mentor.bio;
      _mentorAgeController.text = mentor.age.toString();
      _mentorPriceController.text = mentor.price.toStringAsFixed(2);
      _mentorCategory = mentor.category;
    }

    if (profile.clientProfile != null) {
      final client = profile.clientProfile!;
      _clientWeightController.text = client.weight.toStringAsFixed(1);
      _clientHeightController.text = client.height.toStringAsFixed(1);
      _clientAgeController.text = client.age.toString();
      _clientFitnessLevelController.text = client.fitnessLevel;
    }
  }

  UserProfileUpsertRequest _buildRequest(AppRole role) {
    UpsertMentorProfileData? mentorProfile;
    UpsertClientProfileData? clientProfile;

    if (role == AppRole.mentor) {
      mentorProfile = UpsertMentorProfileData(
        bio: _mentorBioController.text.trim(),
        age: _parseInt(_mentorAgeController.text, 'Mentor age'),
        category: _mentorCategory,
        price: _parseDouble(_mentorPriceController.text, 'Mentor price'),
      );
    }

    if (role == AppRole.client) {
      clientProfile = UpsertClientProfileData(
        weight: _parseDouble(_clientWeightController.text, 'Client weight'),
        height: _parseDouble(_clientHeightController.text, 'Client height'),
        age: _parseInt(_clientAgeController.text, 'Client age'),
        fitnessLevel: _clientFitnessLevelController.text.trim(),
      );
    }

    return UserProfileUpsertRequest(
      firstName: _firstNameController.text.trim(),
      lastName: _lastNameController.text.trim(),
      email: _emailController.text.trim(),
      profileImageUrl: _profileImageUrlController.text.trim().isEmpty
          ? null
          : _profileImageUrlController.text.trim(),
      mentorProfile: mentorProfile,
      clientProfile: clientProfile,
    );
  }

  Future<void> _saveChanges() async {
    await _submit((token, request) {
      return _profileApiService.updateProfile(accessToken: token, request: request);
    }, successMessage: 'Changes saved.');
  }

  Future<void> _submit(
    Future<UserProfile> Function(String token, UserProfileUpsertRequest request) requestBuilder, {
    required String successMessage,
  }) async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    final session = context.read<SessionController>();
    final token = session.accessToken;
    if (token == null) {
      return;
    }

    setState(() {
      _isSaving = true;
      _errorMessage = null;
      _infoMessage = null;
    });

    try {
      final request = _buildRequest(_currentRole());
      final profile = await requestBuilder(token, request);
      _applyProfile(profile);
      await session.syncUserFromProfile(profile);
      if (!mounted) {
        return;
      }

      setState(() {
        _infoMessage = successMessage;
      });
    } catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _errorMessage = _extractErrorMessage(error, fallback: 'Profile request failed.');
      });
    } finally {
      if (mounted) {
        setState(() {
          _isSaving = false;
        });
      }
    }
  }

  int _parseInt(String rawValue, String fieldLabel) {
    final parsed = int.tryParse(rawValue.trim());
    if (parsed == null) {
      throw FormatException('$fieldLabel must be a valid integer.');
    }

    return parsed;
  }

  double _parseDouble(String rawValue, String fieldLabel) {
    final parsed = double.tryParse(rawValue.trim());
    if (parsed == null) {
      throw FormatException('$fieldLabel must be a valid number.');
    }

    return parsed;
  }

  String _extractErrorMessage(Object error, {required String fallback}) {
    if (error is FormatException) {
      return error.message.toString();
    }

    if (error is DioException) {
      final responseData = error.response?.data;
      if (responseData is Map<String, dynamic>) {
        final message = responseData['message'];
        if (message is String && message.trim().isNotEmpty) {
          return message;
        }
      }

      return error.message ?? fallback;
    }

    return fallback;
  }

  @override
  Widget build(BuildContext context) {
    final role = _currentRole();
    final isBusy = _isLoading || _isSaving;
    const borderColor = Color(0x40FFD700);

    return Scaffold(
      appBar: AppBar(
        title: const Text('User Profile'),
      ),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 760),
            child: Container(
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                color: const Color(0xFF2D2D2D),
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: borderColor),
              ),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Role: ${role.name.toUpperCase()}',
                      style: const TextStyle(
                        color: Color(0xFFFFD700),
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _firstNameController,
                      decoration: const InputDecoration(labelText: 'First name'),
                      validator: (value) => (value == null || value.trim().isEmpty)
                          ? 'First name is required.'
                          : null,
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _lastNameController,
                      decoration: const InputDecoration(labelText: 'Last name'),
                      validator: (value) => (value == null || value.trim().isEmpty)
                          ? 'Last name is required.'
                          : null,
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _emailController,
                      decoration: const InputDecoration(labelText: 'Email'),
                      validator: (value) => (value == null || value.trim().isEmpty)
                          ? 'Email is required.'
                          : null,
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _profileImageUrlController,
                      decoration: const InputDecoration(labelText: 'Profile image URL (optional)'),
                    ),
                    if (role == AppRole.mentor) ...[
                      const SizedBox(height: 18),
                      const Divider(color: borderColor),
                      const SizedBox(height: 6),
                      const Text(
                        'Mentor profile',
                        style: TextStyle(fontWeight: FontWeight.w600),
                      ),
                      const SizedBox(height: 10),
                      DropdownButtonFormField<String>(
                        value: _mentorCategory,
                        items: const [
                          DropdownMenuItem(value: 'Weightlifting', child: Text('Weightlifting')),
                          DropdownMenuItem(value: 'Calisthenics', child: Text('Calisthenics')),
                          DropdownMenuItem(value: 'Hybrid', child: Text('Hybrid')),
                        ],
                        decoration: const InputDecoration(labelText: 'Category'),
                        onChanged: isBusy
                            ? null
                            : (value) {
                                if (value == null) {
                                  return;
                                }

                                setState(() {
                                  _mentorCategory = value;
                                });
                              },
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _mentorAgeController,
                        decoration: const InputDecoration(labelText: 'Age'),
                        keyboardType: TextInputType.number,
                        validator: (value) => (value == null || value.trim().isEmpty)
                            ? 'Mentor age is required.'
                            : null,
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _mentorPriceController,
                        decoration: const InputDecoration(labelText: 'Price'),
                        keyboardType: const TextInputType.numberWithOptions(decimal: true),
                        validator: (value) => (value == null || value.trim().isEmpty)
                            ? 'Mentor price is required.'
                            : null,
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _mentorBioController,
                        maxLines: 3,
                        decoration: const InputDecoration(labelText: 'Bio'),
                        validator: (value) => (value == null || value.trim().isEmpty)
                            ? 'Mentor bio is required.'
                            : null,
                      ),
                    ],
                    if (role == AppRole.client) ...[
                      const SizedBox(height: 18),
                      const Divider(color: borderColor),
                      const SizedBox(height: 6),
                      const Text(
                        'Client profile',
                        style: TextStyle(fontWeight: FontWeight.w600),
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _clientWeightController,
                        decoration: const InputDecoration(labelText: 'Weight'),
                        keyboardType: const TextInputType.numberWithOptions(decimal: true),
                        validator: (value) => (value == null || value.trim().isEmpty)
                            ? 'Client weight is required.'
                            : null,
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _clientHeightController,
                        decoration: const InputDecoration(labelText: 'Height'),
                        keyboardType: const TextInputType.numberWithOptions(decimal: true),
                        validator: (value) => (value == null || value.trim().isEmpty)
                            ? 'Client height is required.'
                            : null,
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _clientAgeController,
                        decoration: const InputDecoration(labelText: 'Age'),
                        keyboardType: TextInputType.number,
                        validator: (value) => (value == null || value.trim().isEmpty)
                            ? 'Client age is required.'
                            : null,
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _clientFitnessLevelController,
                        decoration: const InputDecoration(labelText: 'Fitness level'),
                        validator: (value) => (value == null || value.trim().isEmpty)
                            ? 'Fitness level is required.'
                            : null,
                      ),
                    ],
                    if (_errorMessage != null) ...[
                      const SizedBox(height: 14),
                      Text(
                        _errorMessage!,
                        style: const TextStyle(color: Colors.redAccent),
                      ),
                    ],
                    if (_infoMessage != null) ...[
                      const SizedBox(height: 14),
                      Text(
                        _infoMessage!,
                        style: const TextStyle(color: Color(0xFFFFD700)),
                      ),
                    ],
                    const SizedBox(height: 18),
                    Wrap(
                      spacing: 10,
                      runSpacing: 10,
                      children: [
                        ElevatedButton(
                          onPressed: isBusy ? null : _saveChanges,
                          child: const Text('Save Changes'),
                        ),
                      ],
                    ),
                    if (isBusy) ...[
                      const SizedBox(height: 14),
                      const LinearProgressIndicator(),
                    ],
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
