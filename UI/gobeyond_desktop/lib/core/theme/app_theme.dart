import 'package:flutter/material.dart';

/// Dark theme with yellow accents, matching the approved mockups
/// (docs/requirements/mockups/01-06). Kept in one place so every screen
/// looks consistent.
class AppColors {
  const AppColors._();

  static const background = Color(0xFF222222);
  static const panel = Color(0xFF3A3A3A);
  static const panelLight = Color(0xFF4A4A4A);
  static const panelDark = Color(0xFF2B2B2B);
  static const accent = Color(0xFFFFC400);
  static const accentDark = Color(0xFFCC9E00);
  static const textWhite = Color(0xFFFFFFFF);
  static const textMuted = Color(0xFFBDBDBD);
  static const danger = Color(0xFFE05353);
  static const success = Color(0xFF4CAF50);
  static const info = Color(0xFF4FA8E0);
}

class AppTheme {
  const AppTheme._();

  static ThemeData get dark {
    final base = ThemeData.dark(useMaterial3: true);
    return base.copyWith(
      scaffoldBackgroundColor: AppColors.background,
      colorScheme: base.colorScheme.copyWith(
        primary: AppColors.accent,
        secondary: AppColors.accent,
        surface: AppColors.panel,
        error: AppColors.danger,
      ),
      dividerColor: Colors.white24,
      textTheme: base.textTheme.apply(
        bodyColor: AppColors.textWhite,
        displayColor: AppColors.textWhite,
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: AppColors.panelDark,
        contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: BorderSide.none,
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: Colors.white24),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: AppColors.accent, width: 1.6),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: AppColors.danger),
        ),
        focusedErrorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: AppColors.danger, width: 1.6),
        ),
        labelStyle: const TextStyle(color: AppColors.textMuted),
        errorStyle: const TextStyle(color: AppColors.danger, fontWeight: FontWeight.w600),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: AppColors.accent,
          foregroundColor: Colors.black,
          disabledBackgroundColor: AppColors.accent.withValues(alpha: 0.35),
          disabledForegroundColor: Colors.black45,
          textStyle: const TextStyle(fontWeight: FontWeight.bold, letterSpacing: 0.4),
          padding: const EdgeInsets.symmetric(horizontal: 22, vertical: 14),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(28)),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: AppColors.textWhite,
          side: const BorderSide(color: Colors.white38),
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(28)),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(foregroundColor: AppColors.accent),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: AppColors.panel,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
      ),
      cardTheme: CardThemeData(
        color: AppColors.panel,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
      ),
      snackBarTheme: SnackBarThemeData(
        backgroundColor: AppColors.panelLight,
        contentTextStyle: const TextStyle(color: AppColors.textWhite),
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      ),
      checkboxTheme: CheckboxThemeData(
        fillColor: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected) ? AppColors.accent : Colors.transparent,
        ),
        checkColor: const WidgetStatePropertyAll(Colors.black),
      ),
      switchTheme: SwitchThemeData(
        thumbColor: const WidgetStatePropertyAll(Colors.white),
        trackColor: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected) ? AppColors.accent : Colors.white24,
        ),
      ),
      progressIndicatorTheme: const ProgressIndicatorThemeData(color: AppColors.accent),
      dataTableTheme: DataTableThemeData(
        headingRowColor: const WidgetStatePropertyAll(AppColors.panelDark),
        dataRowColor: const WidgetStatePropertyAll(AppColors.panel),
        headingTextStyle: const TextStyle(color: AppColors.accent, fontWeight: FontWeight.bold),
        dataTextStyle: const TextStyle(color: AppColors.textWhite),
      ),
      tooltipTheme: TooltipThemeData(
        decoration: BoxDecoration(color: Colors.black87, borderRadius: BorderRadius.circular(6)),
        textStyle: const TextStyle(color: Colors.white),
      ),
    );
  }
}
