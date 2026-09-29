import 'package:flutter_stripe/flutter_stripe.dart';

import 'app_theme.dart';

/// Stripe PaymentSheet appearance matching [AppTheme]'s dark background
/// (#222222) and yellow accent (#FFC400), so the native sheet doesn't jump
/// out as a plain white/default Stripe screen inside an otherwise fully
/// themed dark app. Shared by every screen that opens the PaymentSheet
/// (initial payment, renewal, resume of a pending payment).
final paymentSheetAppearance = PaymentSheetAppearance(
  colors: const PaymentSheetAppearanceColors(
    primary: AppTheme.accent,
    background: AppTheme.background,
    componentBackground: AppTheme.panel,
    componentBorder: AppTheme.panelLight,
    componentText: AppTheme.textPrimary,
    primaryText: AppTheme.textPrimary,
    secondaryText: AppTheme.textMuted,
    placeholderText: AppTheme.textMuted,
    icon: AppTheme.textMuted,
    error: AppTheme.danger,
  ),
  shapes: const PaymentSheetShape(borderRadius: AppTheme.panelRadius),
  primaryButton: PaymentSheetPrimaryButtonAppearance(
    colors: PaymentSheetPrimaryButtonTheme(
      dark: const PaymentSheetPrimaryButtonThemeColors(
        background: AppTheme.accent,
        text: AppTheme.onAccent,
      ),
      light: const PaymentSheetPrimaryButtonThemeColors(
        background: AppTheme.accent,
        text: AppTheme.onAccent,
      ),
    ),
  ),
);
