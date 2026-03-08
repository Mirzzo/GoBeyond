import 'package:flutter/material.dart';

class MentorListScreen extends StatelessWidget {
  const MentorListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Mentor List')),
      body: const Center(child: Text('Search + sort mentor cards view.')),
    );
  }
}
