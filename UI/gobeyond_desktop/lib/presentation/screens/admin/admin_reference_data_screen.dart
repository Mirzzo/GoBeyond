import 'package:flutter/material.dart';

import '../../../core/services/reference_data_service.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/api_error.dart';
import '../../../core/utils/server_errors.dart';
import '../../../core/utils/validators.dart';
import '../../widgets/dialogs.dart';
import '../../widgets/panel.dart';

enum _DescriptionMode { required, optional, none }

class _ResourceSpec {
  const _ResourceSpec({
    required this.resource,
    required this.label,
    required this.nameMin,
    required this.nameMax,
    required this.descriptionMode,
    this.descriptionMax = 500,
    this.hasSortOrder = false,
  });

  final ReferenceResource resource;
  final String label;
  final int nameMin;
  final int nameMax;
  final _DescriptionMode descriptionMode;
  final int descriptionMax;
  final bool hasSortOrder;
}

const _specs = [
  _ResourceSpec(resource: ReferenceResource.trainingTypes, label: 'Vrste treninga', nameMin: 2, nameMax: 50, descriptionMode: _DescriptionMode.required, descriptionMax: 500),
  _ResourceSpec(resource: ReferenceResource.fitnessGoals, label: 'Ciljevi', nameMin: 2, nameMax: 60, descriptionMode: _DescriptionMode.optional, descriptionMax: 300),
  _ResourceSpec(resource: ReferenceResource.fitnessLevels, label: 'Nivoi spreme', nameMin: 2, nameMax: 40, descriptionMode: _DescriptionMode.optional, descriptionMax: 300, hasSortOrder: true),
  _ResourceSpec(resource: ReferenceResource.genders, label: 'Spolovi', nameMin: 2, nameMax: 30, descriptionMode: _DescriptionMode.none),
];

/// ŠIFARNICI — Vrste treninga / Ciljevi / Nivoi spreme / Spolovi.
class AdminReferenceDataScreen extends StatefulWidget {
  const AdminReferenceDataScreen({super.key});

  @override
  State<AdminReferenceDataScreen> createState() => _AdminReferenceDataScreenState();
}

class _AdminReferenceDataScreenState extends State<AdminReferenceDataScreen> with SingleTickerProviderStateMixin {
  late final TabController _tabController;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: _specs.length, vsync: this);
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ContentPanel(
      title: 'ŠIFARNICI',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TabBar(
            controller: _tabController,
            isScrollable: true,
            labelColor: AppColors.accent,
            unselectedLabelColor: Colors.white70,
            indicatorColor: AppColors.accent,
            tabs: _specs.map((s) => Tab(text: s.label.toUpperCase())).toList(),
          ),
          const SizedBox(height: 16),
          Expanded(
            child: TabBarView(
              controller: _tabController,
              children: _specs.map((s) => _ReferenceTab(spec: s)).toList(),
            ),
          ),
        ],
      ),
    );
  }
}

class _ReferenceTab extends StatefulWidget {
  const _ReferenceTab({required this.spec});
  final _ResourceSpec spec;

  @override
  State<_ReferenceTab> createState() => _ReferenceTabState();
}

class _ReferenceTabState extends State<_ReferenceTab> with AutomaticKeepAliveClientMixin {
  final _service = ReferenceDataService();
  final _searchController = TextEditingController();
  bool _loading = true;
  List<Map<String, dynamic>> _items = const [];

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final items = await _service.list(widget.spec.resource, name: _searchController.text);
      if (widget.spec.hasSortOrder) {
        items.sort((a, b) => ((a['sortOrder'] as int?) ?? 0).compareTo((b['sortOrder'] as int?) ?? 0));
      }
      if (!mounted) return;
      setState(() {
        _items = items;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _openForm({Map<String, dynamic>? existing}) async {
    final saved = await showDialog<bool>(
      context: context,
      builder: (_) => _ReferenceItemDialog(spec: widget.spec, service: _service, existing: existing),
    );

    if (saved == true) {
      if (!mounted) return;
      showSuccessSnack(context,
          existing == null ? '${widget.spec.label} — stavka je dodana.' : '${widget.spec.label} — stavka je sačuvana.');
      _load();
    }
  }

  Future<void> _delete(Map<String, dynamic> item) async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Obriši stavku',
      message: 'Da li ste sigurni da želite obrisati "${item['name']}"?',
      confirmLabel: 'OBRIŠI',
      danger: true,
    );
    if (!confirmed) return;
    try {
      await _service.delete(widget.spec.resource, item['id'] as int);
      if (!mounted) return;
      showSuccessSnack(context, 'Stavka "${item['name']}" je obrisana.');
      _load();
    } catch (error) {
      if (!mounted) return;
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(children: [
          Expanded(child: SearchField(controller: _searchController, hintText: 'Pretraga po nazivu', onSubmitted: (_) => _load())),
          const SizedBox(width: 12),
          ElevatedButton.icon(onPressed: () => _openForm(), icon: const Icon(Icons.add), label: const Text('DODAJ')),
        ]),
        const SizedBox(height: 16),
        Expanded(
          child: _loading
              ? const Center(child: CircularProgressIndicator())
              : _items.isEmpty
                  ? const EmptyState(message: 'Nema stavki.')
                  : ListView.separated(
                      itemCount: _items.length,
                      separatorBuilder: (_, _) => const SizedBox(height: 8),
                      itemBuilder: (context, index) {
                        final item = _items[index];
                        return Container(
                          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                          decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(12)),
                          child: Row(
                            children: [
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(item['name'] as String? ?? '', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                                    if ((item['description'] as String?)?.isNotEmpty == true)
                                      Text(item['description'] as String, style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
                                  ],
                                ),
                              ),
                              PillButton(label: 'UREDI', dense: true, onPressed: () => _openForm(existing: item)),
                              const SizedBox(width: 8),
                              PillButton(label: 'OBRIŠI', dense: true, color: AppColors.danger, onPressed: () => _delete(item)),
                            ],
                          ),
                        );
                      },
                    ),
        ),
      ],
    );
  }
}

/// The DODAJ/UREDI form for one šifarnik entry. Its own [StatefulWidget] so
/// its [TextEditingController]s are created in `initState` and disposed in
/// `dispose` — called only once the dialog route is actually removed,
/// unlike disposing them by hand right after `await showDialog(...)`
/// returns, which raced the dialog's exit transition and threw "A
/// TextEditingController was used after being disposed."
class _ReferenceItemDialog extends StatefulWidget {
  const _ReferenceItemDialog({required this.spec, required this.service, this.existing});

  final _ResourceSpec spec;
  final ReferenceDataService service;
  final Map<String, dynamic>? existing;

  @override
  State<_ReferenceItemDialog> createState() => _ReferenceItemDialogState();
}

class _ReferenceItemDialogState extends State<_ReferenceItemDialog> {
  final _formKey = GlobalKey<FormState>();
  final _serverErrors = ServerErrors();
  late final _nameController = TextEditingController(text: widget.existing?['name'] as String? ?? '');
  late final _descriptionController = TextEditingController(text: widget.existing?['description'] as String? ?? '');
  late final _sortOrderController = TextEditingController(text: (widget.existing?['sortOrder'] ?? 1).toString());

  @override
  void dispose() {
    _nameController.dispose();
    _descriptionController.dispose();
    _sortOrderController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    final spec = widget.spec;
    final payload = <String, dynamic>{'name': _nameController.text.trim()};
    if (spec.descriptionMode != _DescriptionMode.none) {
      payload['description'] = _descriptionController.text.trim().isEmpty ? null : _descriptionController.text.trim();
    }
    if (spec.hasSortOrder) {
      payload['sortOrder'] = int.tryParse(_sortOrderController.text.trim()) ?? 1;
    }
    try {
      if (widget.existing == null) {
        await widget.service.create(spec.resource, payload);
      } else {
        await widget.service.update(spec.resource, widget.existing!['id'] as int, payload);
      }
      if (!mounted) return;
      Navigator.of(context).pop(true);
    } catch (error) {
      if (!mounted) return;
      final apiError = ApiError.from(error, fallback: 'Čuvanje nije uspjelo.');
      setState(() => _serverErrors.apply(apiError.fieldErrors));
      _formKey.currentState!.validate();
      showErrorSnack(context, apiError.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final spec = widget.spec;
    final existing = widget.existing;
    return GbDialog(
      title: existing == null ? 'Dodaj — ${spec.label}' : 'Uredi — ${spec.label}',
      width: 480,
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Odustani')),
        const SizedBox(width: 8),
        ElevatedButton(onPressed: _submit, child: const Text('SAČUVAJ')),
      ],
      child: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextFormField(
              controller: _nameController,
              decoration: const InputDecoration(labelText: 'Naziv'),
              validator: _serverErrors.wrap('name', (v) => Validators.lengthRange(v, spec.nameMin, spec.nameMax, label: 'Naziv')),
            ),
            if (spec.descriptionMode != _DescriptionMode.none) ...[
              const SizedBox(height: 14),
              TextFormField(
                controller: _descriptionController,
                maxLines: 3,
                decoration: InputDecoration(labelText: spec.descriptionMode == _DescriptionMode.required ? 'Opis' : 'Opis (opciono)'),
                validator: _serverErrors.wrap(
                  'description',
                  (v) => spec.descriptionMode == _DescriptionMode.required
                      ? Validators.lengthRange(v, 1, spec.descriptionMax, label: 'Opis')
                      : Validators.optionalLengthRange(v, 0, spec.descriptionMax, label: 'Opis'),
                ),
              ),
            ],
            if (spec.hasSortOrder) ...[
              const SizedBox(height: 14),
              TextFormField(
                controller: _sortOrderController,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'Redoslijed prikaza (1-100)'),
                validator: _serverErrors.wrap('sortOrder', (v) => Validators.numberRange(v, 1, 100, label: 'Redoslijed', isInt: true)),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
