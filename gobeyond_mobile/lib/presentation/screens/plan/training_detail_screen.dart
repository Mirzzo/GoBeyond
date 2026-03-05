import 'package:flutter/material.dart';

class TrainingDetailScreen extends StatelessWidget {
  const TrainingDetailScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Training Detail')),
      body: const Center(child: Text('Full scrollable training instructions.')),
    );
  }
}
