import 'package:flutter/material.dart';
import '../../widgets/category_card.dart';

class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('GoBeyond')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: const [
          CategoryCard(title: 'Weightlifting'),
          SizedBox(height: 12),
          CategoryCard(title: 'Calisthenics'),
          SizedBox(height: 12),
          CategoryCard(title: 'Hybrid'),
        ],
      ),
    );
  }
}
