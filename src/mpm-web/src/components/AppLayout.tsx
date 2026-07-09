import { useState } from 'react';
import { Layout, Avatar, Dropdown, Typography, Tooltip, Badge } from 'antd';
import {
  FileTextOutlined, BarChartOutlined, BellOutlined, MessageOutlined,
  LogoutOutlined, DatabaseOutlined, MenuFoldOutlined, MenuUnfoldOutlined,
  UserOutlined, SettingOutlined, DownOutlined, NotificationOutlined, TeamOutlined,
} from '@ant-design/icons';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import NotificationBell from './NotificationBell';


const { Sider, Content } = Layout;

const NAV_ITEMS = [
  { key: '/licitaciones', icon: <FileTextOutlined />, label: 'Licitaciones', badge: null },
  { key: '/catalogos', icon: <DatabaseOutlined />, label: 'Catálogos', badge: null },
  { key: '/analisis', icon: <BarChartOutlined />, label: 'Análisis', badge: null },
  { key: '/analisis/ejecutivo', icon: <BarChartOutlined />, label: 'Ejecutivo', badge: null },
  { key: '/mensajes', icon: <MessageOutlined />, label: 'Mensajes', badge: null },
  { key: '/notificaciones', icon: <BellOutlined />, label: 'Notificaciones', disabled: false, badge: null },
  { key: '/alertas', icon: <NotificationOutlined />, label: 'Alertas', badge: null },
  { key: '/competidores', icon: <TeamOutlined />, label: 'Competidores', badge: null },
];

export function AppLayout({ children }: { children: React.ReactNode }) {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuth();
  const [collapsed, setCollapsed] = useState(false);

  const initials = user?.nombre
    ? user.nombre.split(' ').map((n: string) => n[0]).slice(0, 2).join('').toUpperCase()
    : 'U';

  const userMenuItems = [
    {
      key: 'profile',
      icon: <UserOutlined />,
      label: <span style={{ fontWeight: 500 }}>Mi perfil</span>,
    },
    {
      key: 'settings',
      icon: <SettingOutlined />,
      label: <span style={{ fontWeight: 500 }}>Configuración</span>,
    },
    { type: 'divider' as const },
    {
      key: 'logout',
      icon: <LogoutOutlined />,
      label: <span style={{ color: '#ef4444', fontWeight: 500 }}>Cerrar sesión</span>,
      onClick: logout,
    },
  ];

  return (
    <Layout style={{ minHeight: '100vh', background: 'var(--bg-base)' }}>
      {/* ---- Sidebar ---- */}
      <Sider
        collapsible
        collapsed={collapsed}
        onCollapse={setCollapsed}
        width={240}
        collapsedWidth={72}
        trigger={null}
        style={{
          background: 'linear-gradient(180deg, #0f172a 0%, #1a2744 100%)',
          boxShadow: '2px 0 20px rgba(0,0,0,0.15)',
          position: 'fixed',
          left: 0,
          top: 0,
          bottom: 0,
          zIndex: 200,
          overflow: 'hidden',
        }}
      >
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          height: '100%',
        }}
      >
        {/* Logo + Collapse header */}
        <div
          style={{
            height: 64,
            display: 'flex',
            alignItems: 'center',
            padding: '0 12px',
            borderBottom: '1px solid rgba(255,255,255,0.06)',
            gap: 4,
            flexShrink: 0,
          }}
        >
          <div
            onClick={() => navigate('/licitaciones')}
            style={{
              flex: 1,
              display: 'flex',
              alignItems: 'center',
              gap: collapsed ? 0 : 12,
              justifyContent: collapsed ? 'center' : 'flex-start',
              cursor: 'pointer',
              minWidth: 0,
              overflow: 'hidden',
            }}
          >
            <img
              src="/images/icon_tivit.svg"
              alt="TIVIT"
              style={{
                width: 36,
                height: 36,
                flexShrink: 0,
                borderRadius: 10,
              }}
            />
            {!collapsed && (
              <div style={{ overflow: 'hidden', minWidth: 0 }}>
                <Typography.Text
                  strong
                  style={{
                    color: '#ffffff',
                    fontSize: 17,
                    letterSpacing: '-0.02em',
                    display: 'block',
                    lineHeight: 1.2,
                    whiteSpace: 'nowrap',
                  }}
                >
                  TIVIT
                </Typography.Text>
                <Typography.Text
                  style={{
                    color: '#94a3b8',
                    fontSize: 10,
                    letterSpacing: '0.08em',
                    textTransform: 'uppercase',
                    whiteSpace: 'nowrap',
                  }}
                >
                  Mercado Público
                </Typography.Text>
              </div>
            )}
          </div>
          <button
            onClick={() => setCollapsed(!collapsed)}
            aria-label={collapsed ? 'Expandir menú' : 'Colapsar menú'}
            aria-expanded={!collapsed}
            style={{
              width: 36,
              height: 36,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              borderRadius: 10,
              cursor: 'pointer',
              color: '#94a3b8',
              fontSize: 16,
              background: 'transparent',
              border: 'none',
              transition: 'all 0.15s',
              flexShrink: 0,
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.background = 'rgba(255,255,255,0.07)';
              e.currentTarget.style.color = '#e2e8f0';
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.background = 'transparent';
              e.currentTarget.style.color = '#94a3b8';
            }}
          >
            {collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
          </button>
        </div>

        {/* Nav items */}
        <nav style={{ flex: 1, padding: '12px 0', overflowY: 'auto' }}>
          <div
            style={{
              padding: collapsed ? '0' : '0 10px 8px',
              fontSize: 10,
              fontWeight: 600,
              textTransform: 'uppercase',
              letterSpacing: '0.1em',
              color: '#94a3b8',
              transition: 'all 0.25s',
            }}
          >
            {!collapsed && 'Navegación'}
          </div>
          {NAV_ITEMS.map((item) => {
            const hasMoreSpecificMatch = NAV_ITEMS.some(
              other => other.key !== item.key &&
                other.key.startsWith(item.key) &&
                location.pathname.startsWith(other.key)
            );
            const isActive = location.pathname === item.key ||
              (item.key !== '/' && location.pathname.startsWith(item.key + '/') && !hasMoreSpecificMatch);
            const isDisabled = item.disabled;

            const navItem = (
              <div
                key={item.key}
                onClick={() => !isDisabled && navigate(item.key)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 12,
                  padding: collapsed ? '12px 0' : '10px 14px',
                  margin: '2px 8px',
                  borderRadius: 10,
                  cursor: isDisabled ? 'not-allowed' : 'pointer',
                  background: isActive
                    ? 'rgba(227, 6, 19, 0.18)'
                    : 'transparent',
                  borderLeft: isActive ? '3px solid #E30613' : '3px solid transparent',
                  transition: 'all 0.15s cubic-bezier(0.4, 0, 0.2, 1)',
                  color: isDisabled ? '#64748b' : 'inherit',
                  justifyContent: collapsed ? 'center' : 'flex-start',
                  position: 'relative',
                }}
                onMouseEnter={(e) => {
                  if (!isActive && !isDisabled) {
                    e.currentTarget.style.background = 'rgba(255,255,255,0.07)';
                  }
                }}
                onMouseLeave={(e) => {
                  if (!isActive && !isDisabled) {
                    e.currentTarget.style.background = 'transparent';
                  }
                }}
              >
                <span
                  style={{
                    fontSize: 18,
                    color: isActive ? '#E30613' : 'rgba(255,255,255,0.65)',
                    display: 'flex',
                    alignItems: 'center',
                    transition: 'all 0.15s',
                    flexShrink: 0,
                  }}
                >
                  {item.badge ? (
                    <Badge count={item.badge} size="small">
                      {item.icon}
                    </Badge>
                  ) : item.icon}
                </span>
                {!collapsed && (
                  <span
                    style={{
                      fontSize: 14,
                      fontWeight: isActive ? 600 : 500,
                      color: isActive ? '#ffffff' : '#cbd5e1',
                      whiteSpace: 'nowrap',
                      transition: 'all 0.15s',
                    }}
                  >
                    {item.label}
                  </span>
                )}
              </div>
            );

            return collapsed ? (
              <Tooltip key={item.key} title={item.label} placement="right">
                {navItem}
              </Tooltip>
            ) : navItem;
          })}
        </nav>

        {/* User info */}
        <div
          style={{
            borderTop: '1px solid rgba(255,255,255,0.06)',
            padding: collapsed ? '8px' : '10px 12px',
            flexShrink: 0,
          }}
        >
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 10,
              justifyContent: collapsed ? 'center' : 'flex-start',
            }}
          >
            <Avatar
              size={collapsed ? 32 : 30}
              shape="circle"
              style={{
                background: 'linear-gradient(135deg, #E30613, #ff3a46)',
                fontSize: 11,
                fontWeight: 700,
                flexShrink: 0,
              }}
            >
              {initials}
            </Avatar>
            {!collapsed && (
              <div style={{ overflow: 'hidden', minWidth: 0 }}>
                <div
                  style={{
                    fontSize: 12,
                    fontWeight: 600,
                    color: '#cbd5e1',
                    whiteSpace: 'nowrap',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                  }}
                >
                  {user?.nombre ?? 'Usuario'}
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
      </Sider>

      {/* ---- Main layout ---- */}
      <Layout
        style={{
          marginLeft: collapsed ? 72 : 240,
          transition: 'margin-left 0.25s cubic-bezier(0.4, 0, 0.2, 1)',
          background: 'var(--bg-base)',
          minHeight: '100vh',
        }}
      >
        {/* ---- Header ---- */}
        <div
          style={{
            height: 64,
            background: '#ffffff',
            borderBottom: '1px solid var(--border)',
            boxShadow: 'var(--shadow-header)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'flex-end',
            padding: '0 24px',
            position: 'sticky',
            top: 0,
            zIndex: 100,
            gap: 8,
          }}
        >
          {/* Notification bell */}
          <NotificationBell />

          {/* Divider */}
          <div
            style={{
              width: 1,
              height: 24,
              background: 'var(--border)',
              margin: '0 4px',
            }}
          />

          {/* User dropdown */}
          <Dropdown menu={{ items: userMenuItems }} placement="bottomRight" trigger={['click']}>
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 8,
                padding: '6px 10px',
                borderRadius: 10,
                cursor: 'pointer',
                transition: 'all 0.15s',
                border: '1px solid transparent',
              }}
              onMouseEnter={(e) => {
                e.currentTarget.style.background = 'var(--bg-muted)';
                e.currentTarget.style.borderColor = 'var(--border)';
              }}
              onMouseLeave={(e) => {
                e.currentTarget.style.background = 'transparent';
                e.currentTarget.style.borderColor = 'transparent';
              }}
            >
              <Avatar
                size={32}
                style={{
                  background: 'linear-gradient(135deg, #E30613, #ff3a46)',
                  fontSize: 12,
                  fontWeight: 700,
                }}
              >
                {initials}
              </Avatar>
              <div style={{ lineHeight: 1.3 }}>
                <div
                  style={{
                    fontSize: 13,
                    fontWeight: 600,
                    color: 'var(--text-primary)',
                    maxWidth: 120,
                    whiteSpace: 'nowrap',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                  }}
                >
                  {user?.nombre ?? 'Usuario'}
                </div>
              </div>
              <DownOutlined style={{ fontSize: 12, color: 'var(--text-muted)' }} />
            </div>
          </Dropdown>
        </div>

        {/* ---- Content ---- */}
        <Content
          style={{
            padding: 24,
            minHeight: 'calc(100vh - 64px)',
          }}
        >
          <div className="mpm-fade-in">
            {children}
          </div>
        </Content>
      </Layout>
    </Layout>
  );
}