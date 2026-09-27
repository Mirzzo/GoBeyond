import 'package:flutter/material.dart';

import '../../core/config/app_config.dart';
import '../../core/theme/app_theme.dart';

/// Renders a profile/mentor photo from a relative API path, falling back to
/// a placeholder person icon when there is none (mockup 02 photo cards).
class GbAvatar extends StatelessWidget {
  const GbAvatar({super.key, this.imageUrl, this.size = 96, this.borderColor});

  final String? imageUrl;
  final double size;
  final Color? borderColor;

  @override
  Widget build(BuildContext context) {
    final resolved = AppConfig.resolveUrl(imageUrl);
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(size * 0.16),
        border: Border.all(color: borderColor ?? AppColors.accent, width: 2.5),
        color: AppColors.panelDark,
      ),
      clipBehavior: Clip.antiAlias,
      child: resolved != null
          ? Image.network(
              resolved,
              fit: BoxFit.cover,
              errorBuilder: (_, _, _) => _placeholder(),
              loadingBuilder: (context, child, progress) =>
                  progress == null ? child : const Center(child: CircularProgressIndicator(strokeWidth: 2)),
            )
          : _placeholder(),
    );
  }

  Widget _placeholder() {
    return Center(child: Icon(Icons.person, size: size * 0.55, color: Colors.white38));
  }
}
