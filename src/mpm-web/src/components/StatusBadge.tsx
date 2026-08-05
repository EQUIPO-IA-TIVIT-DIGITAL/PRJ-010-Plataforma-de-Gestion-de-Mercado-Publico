import { theme } from 'antd';
import type { ReactNode } from 'react';

export type StatusBadgeVariant = 'neutral' | 'info' | 'warning' | 'success' | 'error' | 'tertiary';

export interface StatusBadgeProps {
  variant: StatusBadgeVariant;
  label: string;
  icon?: ReactNode;
}

// Sexta variante ("tertiary") sin slot nativo en Ant Design -- documentada junto al resto de
// la paleta de marca en main.tsx (colorTertiary), no un hex suelto local.
const TERTIARY_COLOR = '#8b5cf6';
const TERTIARY_BG = '#faf5ff';

/**
 * Indicador de estado unico del sistema (spec 019, US1-US5): reemplaza los 5 STATUS_CONFIG/
 * ternarios divergentes encontrados en la auditoria (Analisis, Catalogos, Notificaciones,
 * Alertas, Ejecutivo). El color SIEMPRE viene de `variant`, nunca de un hex por pantalla.
 */
export function StatusBadge({ variant, label, icon }: StatusBadgeProps) {
  const { token } = theme.useToken();

  const palette: Record<StatusBadgeVariant, { color: string; bg: string }> = {
    neutral: { color: token.colorTextSecondary, bg: token.colorFillTertiary },
    info: { color: token.colorInfo, bg: token.colorInfoBg },
    warning: { color: token.colorWarning, bg: token.colorWarningBg },
    success: { color: token.colorSuccess, bg: token.colorSuccessBg },
    error: { color: token.colorError, bg: token.colorErrorBg },
    tertiary: { color: TERTIARY_COLOR, bg: TERTIARY_BG },
  };

  const { color, bg } = palette[variant];

  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 6,
        padding: '2px 10px',
        borderRadius: 999,
        fontSize: token.fontSizeSM,
        fontWeight: 600,
        color,
        background: bg,
        lineHeight: '20px',
        whiteSpace: 'nowrap',
      }}
    >
      {icon}
      {label}
    </span>
  );
}
