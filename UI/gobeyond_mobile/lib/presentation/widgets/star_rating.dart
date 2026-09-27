import 'package:flutter/material.dart';

import '../../core/theme/app_theme.dart';

/// Read-only star row (mentor cards/detail, review list) matching mockup 08.
class StarRating extends StatelessWidget {
  const StarRating(
      {super.key, required this.rating, this.size = 22, this.max = 5});

  final double rating;
  final double size;
  final int max;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: List.generate(max, (index) {
        final threshold = index + 1;
        IconData icon;
        if (rating >= threshold) {
          icon = Icons.star_rounded;
        } else if (rating >= threshold - 0.5) {
          icon = Icons.star_half_rounded;
        } else {
          icon = Icons.star_border_rounded;
        }
        return Icon(icon, color: AppTheme.accent, size: size);
      }),
    );
  }
}

/// Interactive star picker used by the "napiši recenziju" dialog.
class StarRatingInput extends StatelessWidget {
  const StarRatingInput({
    super.key,
    required this.rating,
    required this.onChanged,
    this.size = 34,
  });

  final int rating;
  final ValueChanged<int> onChanged;
  final double size;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: List.generate(5, (index) {
        final value = index + 1;
        return IconButton(
          padding: EdgeInsets.zero,
          constraints: const BoxConstraints(),
          onPressed: () => onChanged(value),
          icon: Icon(
            value <= rating ? Icons.star_rounded : Icons.star_border_rounded,
            color: AppTheme.accent,
            size: size,
          ),
        );
      }),
    );
  }
}
