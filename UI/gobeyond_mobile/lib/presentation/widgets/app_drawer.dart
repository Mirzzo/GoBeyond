import 'package:flutter/material.dart';

class AppDrawer extends StatelessWidget {
  const AppDrawer({super.key});

  @override
  Widget build(BuildContext context) {
    return Drawer(
      child: ListView(
        children: const [
          DrawerHeader(child: Text('GoBeyond')),
          ListTile(title: Text('My Profile')),
          ListTile(title: Text('Home')),
          ListTile(title: Text('My Plan')),
          ListTile(title: Text('Subscription')),
          ListTile(title: Text('About')),
          ListTile(title: Text('Other')),
        ],
      ),
    );
  }
}
