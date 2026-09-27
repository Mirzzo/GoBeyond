import 'package:flutter/material.dart';

import '../../../core/services/admin_service.dart';
import '../../../core/services/reference_data_service.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/api_error.dart';
import '../../widgets/dialogs.dart';
import '../../widgets/panel.dart';
import 'admin_mentor_request_detail_screen.dart';

/// Mockup 01 — ZAHTJEVI ZA MENTORA.
class AdminMentorRequestsScreen extends StatefulWidget {
  const AdminMentorRequestsScreen({super.key});

  @override
  State<AdminMentorRequestsScreen> createState() => _AdminMentorRequestsScreenState();
}

class _AdminMentorRequestsScreenState extends State<AdminMentorRequestsScreen> {
  final _service = AdminService();
  final _referenceDataService = ReferenceDataService();
  final _searchController = TextEditingController();
  bool _loading = true;
  List<Map<String, dynamic>> _requests = const [];
  List<Map<String, dynamic>> _trainingTypes = const [];
  int? _trainingTypeFilter;

  @override
  void initState() {
    super.initState();
    _referenceDataService.list(ReferenceResource.trainingTypes).then((value) {
      if (mounted) setState(() => _trainingTypes = value);
    });
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
      final requests = await _service.getMentorRequests(
        search: _searchController.text,
        trainingTypeId: _trainingTypeFilter,
      );
      if (!mounted) return;
      setState(() {
        _requests = requests;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      showErrorSnack(context, ApiError.from(error).message);
    }
  }

  Future<void> _openDetail(Map<String, dynamic> request) async {
    final changed = await Navigator.of(context).push<bool>(
      MaterialPageRoute(builder: (_) => AdminMentorRequestDetailScreen(mentorProfileId: request['mentorProfileId'] as int)),
    );
    if (changed == true) _load();
  }

  @override
  Widget build(BuildContext context) {
    return ContentPanel(
      title: 'ZAHTJEVI ZA MENTORA',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(children: [
            Expanded(
              child: SearchField(
                controller: _searchController,
                hintText: 'Pretraga po imenu i prezimenu',
                onSubmitted: (_) => _load(),
              ),
            ),
            const SizedBox(width: 12),
            SizedBox(
              width: 220,
              child: DropdownButtonFormField<int?>(
                initialValue: _trainingTypeFilter,
                decoration: const InputDecoration(labelText: 'Vrsta treninga'),
                items: [
                  const DropdownMenuItem(value: null, child: Text('Sve vrste treninga')),
                  ..._trainingTypes.map((t) => DropdownMenuItem(value: t['id'] as int, child: Text(t['name'] as String))),
                ],
                onChanged: (value) {
                  setState(() => _trainingTypeFilter = value);
                  _load();
                },
              ),
            ),
          ]),
          const SizedBox(height: 16),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _requests.isEmpty
                    ? const EmptyState(message: 'Nema zahtjeva za mentorski nalog.')
                    : ListView.separated(
                        itemCount: _requests.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 10),
                        itemBuilder: (context, index) {
                          final request = _requests[index];
                          return Container(
                            padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 14),
                            decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(14)),
                            child: Row(
                              children: [
                                SizedBox(
                                  width: 34,
                                  child: Text('${index + 1}.', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                                ),
                                Expanded(
                                  flex: 3,
                                  child: Text(request['fullName'] as String? ?? '', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                                ),
                                Expanded(
                                  flex: 2,
                                  child: Text((request['trainingTypeName'] as String? ?? '').toUpperCase(),
                                      style: const TextStyle(color: Colors.white)),
                                ),
                                PillButton(label: 'PREGLED..', onPressed: () => _openDetail(request)),
                              ],
                            ),
                          );
                        },
                      ),
          ),
        ],
      ),
    );
  }
}
