import { Typography } from 'antd';
import { theme } from 'antd';
import type { ReactNode } from 'react';

const { Title, Text } = Typography;

export interface PageHeaderProps {
  icon: ReactNode;
  title: string;
  subtitle?: string;
  actions?: ReactNode;
}

/**
 * Header de pagina unico del sistema (spec 019, US1-US5): reemplaza las 3 estructuras
 * divergentes encontradas en la auditoria (icono con gradiente rojo, icono con gradiente
 * morado, sin icono). El chip SIEMPRE usa colorPrimary del theme (rojo TIVIT), no un color
 * libre por pantalla.
 */
export function PageHeader({ icon, title, subtitle, actions }: PageHeaderProps) {
  const { token } = theme.useToken();

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        flexWrap: 'wrap',
        gap: 16,
        marginBottom: 20,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 40,
            height: 40,
            borderRadius: token.borderRadiusLG,
            background: token.colorPrimary,
            color: '#ffffff',
            fontSize: 20,
            flexShrink: 0,
          }}
        >
          {icon}
        </div>
        <div>
          <Title level={4} style={{ margin: 0, lineHeight: '24px' }}>
            {title}
          </Title>
          {subtitle && (
            <Text type="secondary" style={{ fontSize: token.fontSizeSM }}>
              {subtitle}
            </Text>
          )}
        </div>
      </div>
      {actions && <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>{actions}</div>}
    </div>
  );
}
