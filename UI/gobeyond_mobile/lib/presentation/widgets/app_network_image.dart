import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';

import '../../core/constants/app_constants.dart';
import '../../core/theme/app_theme.dart';

/// Resolves a relative API image path (e.g. `/uploads/...`) into an absolute
/// URL against the configured API base, per the contract's "client prefixes
/// relative paths" rule.
String resolveImageUrl(String? path) {
  if (path == null || path.isEmpty) return '';
  if (path.startsWith('http://') || path.startsWith('https://')) return path;
  final base = AppConstants.apiBaseUrl;
  return path.startsWith('/') ? '$base$path' : '$base/$path';
}

/// A mentor/client/progress photo with the mockups' rounded yellow border
/// and a graceful placeholder when there is no photo yet.
class AppNetworkImage extends StatelessWidget {
  const AppNetworkImage({
    super.key,
    required this.url,
    this.width,
    this.height,
    this.borderRadius = 20,
    this.yellowBorder = false,
    this.placeholderIcon = Icons.person_rounded,
  });

  final String? url;
  final double? width;
  final double? height;
  final double borderRadius;
  final bool yellowBorder;
  final IconData placeholderIcon;

  @override
  Widget build(BuildContext context) {
    final resolved = resolveImageUrl(url);

    final content = ClipRRect(
      borderRadius: BorderRadius.circular(borderRadius),
      child: resolved.isEmpty
          ? _placeholder()
          : CachedNetworkImage(
              imageUrl: resolved,
              width: width,
              height: height,
              fit: BoxFit.cover,
              placeholder: (_, __) => _placeholder(loading: true),
              errorWidget: (_, __, ___) => _placeholder(),
            ),
    );

    if (!yellowBorder) return content;

    return Container(
      width: width,
      height: height,
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        border: Border.all(color: AppTheme.accent, width: 3),
        borderRadius: BorderRadius.circular(borderRadius + 4),
      ),
      child: content,
    );
  }

  Widget _placeholder({bool loading = false}) {
    return Container(
      width: width,
      height: height,
      color: AppTheme.panelLight,
      alignment: Alignment.center,
      child: loading
          ? const SizedBox(
              width: 22,
              height: 22,
              child: CircularProgressIndicator(strokeWidth: 2),
            )
          : Icon(placeholderIcon, color: AppTheme.textMuted, size: 36),
    );
  }
}
