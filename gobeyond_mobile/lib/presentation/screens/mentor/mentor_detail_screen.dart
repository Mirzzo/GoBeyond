import 'package:flutter/material.dart';

class MentorDetailScreen extends StatelessWidget {
  const MentorDetailScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Mentor Detail')),
      body: const Center(child: Text('Mentor bio, reviews and Buy Plan button.')),
    );
  }
}
