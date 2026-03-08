import 'package:flutter/material.dart';

class MyPlanScreen extends StatelessWidget {
  const MyPlanScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('My Plan')),
      body: const Center(child: Text('Motivational quote + daily plan card.')),
    );
  }
}
