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
    final spec = widget.spec;
    final formKey = GlobalKey<FormState>();
    final serverErrors = ServerErrors();
    final nameController = TextEditingController(text: existing?['name'] as String? ?? '');
    final descriptionController = TextEditingController(text: existing?['description'] as String? ?? '');
    final sortOrderController = TextEditingController(text: (existing?['sortOrder'] ?? 1).toString());

    final saved = await showGbDialog<bool>(
      context: context,
      title: existing == null ? 'Dodaj — ${spec.label}' : 'Uredi — ${spec.label}',
      width: 480,
      child: Form(
        key: formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextFormField(
              controller: nameController,
              decoration: const InputDecoration(labelText: 'Naziv'),
              validator: serverErrors.wrap('name', (v) => Validators.lengthRange(v, spec.nameMin, spec.nameMax, label: 'Naziv')),
            ),
            if (spec.descriptionMode != _DescriptionMode.none) ...[
              const SizedBox(height: 14),
              TextFormField(
                controller: descriptionController,
                maxLines: 3,
                decoration: InputDecoration(labelText: spec.descriptionMode == _DescriptionMode.required ? 'Opis' : 'Opis (opciono)'),
                validator: serverErrors.wrap(
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
                controller: sortOrderController,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'Redoslijed prikaza (1-100)'),
                validator: serverErrors.wrap('sortOrder', (v) => Validators.numberRange(v, 1, 100, label: 'Redoslijed', isInt: true)),
              ),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Odustani')),
        const SizedBox(width: 8),
        ElevatedButton(
          onPressed: () async {
            if (!formKey.currentState!.validate()) return;
            final payload = <String, dynamic>{'name': nameController.text.trim()};
            if (spec.descriptionMode != _DescriptionMode.none) {
              payload['description'] = descriptionController.text.trim().isEmpty ? null : descriptionController.text.trim();
            }
            if (spec.hasSortOrder) {
              payload['sortOrder'] = int.tryParse(sortOrderController.text.trim()) ?? 1;
            }
            try {
              if (existing == null) {
                await _service.create(spec.resource, payload);
              } else {
                await _service.update(spec.resource, existing['id'] as int, payload);
              }
              if (!mounted) return;
              Navigator.of(context).pop(true);
            } catch (error) {
              final apiError = ApiError.from(error, fallback: 'Čuvanje nije uspjelo.');
              serverErrors.apply(apiError.fieldErrors);
              formKey.currentState!.validate();
              if (!mounted) return;
              showErrorSnack(context, apiError.message);
            }
          },
          child: const Text('SAČUVAJ'),
        ),
      ],
    );

    nameController.dispose();
    descriptionController.dispose();
    sortOrderController.dispose();

    if (saved == true) {
      if (!mounted) return;
      showSuccessSnack(context, existing == null ? '${spec.label} — stavka je dodana.' : '${spec.label} — stavka je sačuvana.');
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
