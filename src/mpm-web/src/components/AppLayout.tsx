import { useState, useEffect } from 'react';
import { Layout, Avatar, Dropdown, Typography, Tooltip, Badge, Modal, Tabs, Input, Button, message, Tag } from 'antd';
import {
  FileTextOutlined, BarChartOutlined, BellOutlined, MessageOutlined,
  LogoutOutlined, DatabaseOutlined, MenuFoldOutlined, MenuUnfoldOutlined,
  UserOutlined, SettingOutlined, DownOutlined, NotificationOutlined, TeamOutlined,
  SendOutlined, LockOutlined, AuditOutlined, SafetyCertificateOutlined,
} from '@ant-design/icons';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { apiPut, apiPost } from '../lib/apiClient';
import NotificationBell from './NotificationBell';
import { AnalisisCompletionWatcher } from './AnalisisCompletionWatcher';


const { Sider, Content } = Layout;

type NavItem = {
  key: string;
  icon: React.ReactNode;
  label: string;
  badge: string | null;
  disabled?: boolean;
  adminOnly?: boolean;
  section?: string;
};

const NAV_ITEMS: NavItem[] = [
  { key: '/licitaciones', icon: <FileTextOutlined />, label: 'Licitaciones', badge: null },
  { key: '/catalogos', icon: <DatabaseOutlined />, label: 'Catálogos', badge: null },
  { key: '/analisis', icon: <BarChartOutlined />, label: 'Análisis', badge: null },
  { key: '/analisis/ejecutivo', icon: <BarChartOutlined />, label: 'Ejecutivo', badge: null },
  { key: '/mensajes', icon: <MessageOutlined />, label: 'Mensajes', badge: null },
  { key: '/notificaciones', icon: <BellOutlined />, label: 'Notificaciones', disabled: false, badge: null },
  { key: '/alertas', icon: <NotificationOutlined />, label: 'Alertas', badge: null },
  { key: '/competidores', icon: <TeamOutlined />, label: 'Competidores', badge: null },
  // Centro de Administración — visible para Admin y SuperAdmin.
  { key: '/admin/usuarios', icon: <SafetyCertificateOutlined />, label: 'Usuarios', adminOnly: true, section: 'Administración', badge: null },
  { key: '/admin/logs', icon: <AuditOutlined />, label: 'Logs y actividad', adminOnly: true, section: 'Administración', badge: null },
  { key: '/admin/config-ia', icon: <SettingOutlined />, label: 'Admin IA', adminOnly: true, section: 'Administración', badge: null },
];

export function AppLayout({ children }: { children: React.ReactNode }) {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout, updateUserLocal } = useAuth();
  const [collapsed, setCollapsed] = useState(false);

  const [settingsModalOpen, setSettingsModalOpen] = useState(false);
  const [activeTab, setActiveTab] = useState<'profile' | 'settings' | 'security'>('profile');
  const [nombre, setNombre] = useState(user?.nombre ?? '');
  const [emailAlertas, setEmailAlertas] = useState('');
  const [telegramChatId, setTelegramChatId] = useState('');
  const [savingProfile, setSavingProfile] = useState(false);
  const [savingEmail, setSavingEmail] = useState(false);
  const [savingTelegram, setSavingTelegram] = useState(false);
  const [generatingTelegramLink, setGeneratingTelegramLink] = useState(false);

  // Password change states
  const [passwordActual, setPasswordActual] = useState('');
  const [nuevaPassword, setNuevaPassword] = useState('');
  const [confirmarPassword, setConfirmarPassword] = useState('');
  const [savingPassword, setSavingPassword] = useState(false);

  // Sync state with user when it changes
  useEffect(() => {
    if (user) {
      setNombre(user.nombre);
    }
  }, [user]);

  const handleUpdatePassword = async () => {
    if (!passwordActual) {
      message.error('Ingresa tu contraseña actual');
      return;
    }
    if (!nuevaPassword || nuevaPassword.length < 6) {
      message.error('La nueva contraseña debe tener al menos 6 caracteres');
      return;
    }
    if (nuevaPassword !== confirmarPassword) {
      message.error('La confirmación de la nueva contraseña no coincide');
      return;
    }

    setSavingPassword(true);
    try {
      await apiPut('/api/v1/usuarios/mi-password', {
        passwordActual,
        nuevaPassword,
        confirmarPassword,
      });
      message.success('Contraseña actualizada correctamente');
      setPasswordActual('');
      setNuevaPassword('');
      setConfirmarPassword('');
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Error al actualizar la contraseña');
    } finally {
      setSavingPassword(false);
    }
  };

  const handleUpdateName = async () => {
    if (!nombre.trim()) {
      message.error('El nombre no puede estar vacío');
      return;
    }
    setSavingProfile(true);
    try {
      await apiPut('/api/v1/usuarios/mi-perfil', { nombre: nombre.trim() });
      updateUserLocal(nombre.trim());
      message.success('Perfil actualizado correctamente');
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Error al actualizar el perfil');
    } finally {
      setSavingProfile(false);
    }
  };

  const handleUpdateEmail = async () => {
    if (!emailAlertas.trim() || !emailAlertas.includes('@')) {
      message.error('Ingresá un correo electrónico válido');
      return;
    }
    setSavingEmail(true);
    try {
      await apiPost('/api/v1/alertas/mi-email', { emailAlertas: emailAlertas.trim() });
      message.success('Canal de correo configurado');
      setEmailAlertas('');
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Error al guardar el correo');
    } finally {
      setSavingEmail(false);
    }
  };

  const handleUpdateTelegram = async () => {
    if (!telegramChatId.trim()) {
      message.error('Ingresá tu Chat ID');
      return;
    }
    setSavingTelegram(true);
    try {
      await apiPost('/api/v1/alertas/mi-telegram', { telegramChatId: telegramChatId.trim() });
      message.success('Chat de Telegram guardado');
      setTelegramChatId('');
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Error al guardar Telegram');
    } finally {
      setSavingTelegram(false);
    }
  };

  const handleLinkTelegram = async () => {
    setGeneratingTelegramLink(true);
    try {
      const res = await apiPost<{ data: { url: string } }>('/api/v1/alertas/mi-telegram/link');
      if (res?.data?.url) {
        window.open(res.data.url, '_blank', 'noopener,noreferrer');
      } else {
        message.error('No se pudo generar el link');
      }
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Error al generar el link');
    } finally {
      setGeneratingTelegramLink(false);
    }
  };

  const handleMenuClick = (e: { key: string }) => {
    if (e.key === 'profile') {
      setActiveTab('profile');
      setSettingsModalOpen(true);
    } else if (e.key === 'settings') {
      setActiveTab('settings');
      setSettingsModalOpen(true);
    } else if (e.key === 'security') {
      setActiveTab('security');
      setSettingsModalOpen(true);
    }
  };

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
    {
      key: 'security',
      icon: <LockOutlined />,
      label: <span style={{ fontWeight: 500 }}>Cambiar contraseña</span>,
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
      <AnalisisCompletionWatcher />
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
          {(() => {
            const esAdmin = user?.roles?.includes('SuperAdmin') || user?.roles?.includes('Admin');
            const nodes: React.ReactNode[] = [];
            let lastSection: string | null = null;
            NAV_ITEMS
              .filter((item) => !item.adminOnly || esAdmin)
              .forEach((item) => {
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

            if (item.section && item.section !== lastSection) {
              lastSection = item.section;
              nodes.push(
                <div
                  key={`section-${item.section}`}
                  style={{
                    padding: collapsed ? '0' : '0 10px 8px',
                    fontSize: 10,
                    fontWeight: 600,
                    textTransform: 'uppercase',
                    letterSpacing: '0.1em',
                    color: '#94a3b8',
                    marginTop: 8,
                  }}
                >
                  {!collapsed && item.section}
                </div>
              );
            }
            nodes.push(
              collapsed ? (
                <Tooltip key={item.key} title={item.label} placement="right">
                  {navItem}
                </Tooltip>
              ) : navItem
            );
            });
            return nodes;
          })()}
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
          <Dropdown menu={{ items: userMenuItems, onClick: handleMenuClick }} placement="bottomRight" trigger={['click']}>
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

      {/* ---- User Profile & Settings Modal ---- */}
      <Modal
        title={
          <span style={{ fontSize: 16, fontWeight: 700, display: 'flex', alignItems: 'center', gap: 8 }}>
            <UserOutlined style={{ color: '#E30613' }} /> Mi Perfil y Configuración
          </span>
        }
        open={settingsModalOpen}
        onCancel={() => setSettingsModalOpen(false)}
        footer={null}
        width={500}
        style={{ borderRadius: 14 }}
      >
        <Tabs
          activeKey={activeTab}
          onChange={(key) => setActiveTab(key as any)}
          items={[
            {
              key: 'profile',
              label: 'Mi Perfil',
              children: (
                <div style={{ padding: '8px 0', display: 'flex', flexDirection: 'column', gap: 16 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 16, paddingBottom: 16, borderBottom: '1px solid var(--border)' }}>
                    <Avatar
                      size={64}
                      style={{
                        background: 'linear-gradient(135deg, #E30613, #ff3a46)',
                        fontSize: 24,
                        fontWeight: 700,
                      }}
                    >
                      {initials}
                    </Avatar>
                    <div>
                      <Typography.Title level={5} style={{ margin: 0, fontWeight: 700 }}>
                        {user?.nombre ?? 'Usuario'}
                      </Typography.Title>
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {user?.email ?? 'correo@tivit.cl'}
                      </Typography.Text>
                      <div style={{ marginTop: 4 }}>
                        {user?.roles?.map((r: string) => (
                          <Tag key={r} color="red" style={{ borderRadius: 6, fontSize: 10 }}>
                            {r.toUpperCase()}
                          </Tag>
                        ))}
                      </div>
                    </div>
                  </div>
                  <div>
                    <Typography.Text strong style={{ fontSize: 13, display: 'block', marginBottom: 8 }}>
                      Modificar Nombre
                    </Typography.Text>
                    <div style={{ display: 'flex', gap: 8 }}>
                      <Input
                        value={nombre}
                        onChange={(e) => setNombre(e.target.value)}
                        placeholder="Ingresa tu nombre..."
                        style={{ borderRadius: 8 }}
                      />
                      <Button
                        type="primary"
                        onClick={handleUpdateName}
                        loading={savingProfile}
                        style={{ borderRadius: 8, background: '#E30613', border: 'none', fontWeight: 600 }}
                      >
                        Guardar
                      </Button>
                    </div>
                  </div>
                </div>
              ),
            },
            {
              key: 'settings',
              label: 'Configuración Alertas',
              children: (
                <div style={{ padding: '8px 0', display: 'flex', flexDirection: 'column', gap: 20 }}>
                  {/* Correo */}
                  <div>
                    <Typography.Text strong style={{ fontSize: 13.5, display: 'block', marginBottom: 2 }}>
                      ✉️ Canal de Correo
                    </Typography.Text>
                    <Typography.Text type="secondary" style={{ fontSize: 11.5, display: 'block', marginBottom: 10 }}>
                      Configura una dirección de correo adicional para recibir alertas detalladas del scraper.
                    </Typography.Text>
                    <div style={{ display: 'flex', gap: 8 }}>
                      <Input
                        value={emailAlertas}
                        onChange={(e) => setEmailAlertas(e.target.value)}
                        placeholder="ej. alertas-tivit@tivit.cl"
                        style={{ borderRadius: 8 }}
                      />
                      <Button
                        type="primary"
                        onClick={handleUpdateEmail}
                        loading={savingEmail}
                        style={{ borderRadius: 8, background: '#E30613', border: 'none', fontWeight: 600 }}
                      >
                        Guardar
                      </Button>
                    </div>
                  </div>
                </div>
              ),
            },
            {
              key: 'security',
              label: 'Seguridad',
              children: (
                <div style={{ padding: '8px 0', display: 'flex', flexDirection: 'column', gap: 16 }}>
                  <div>
                    <Typography.Text strong style={{ fontSize: 13.5, display: 'block', marginBottom: 2 }}>
                      Cambiar Contraseña
                    </Typography.Text>
                    <Typography.Text type="secondary" style={{ fontSize: 11.5, display: 'block', marginBottom: 12 }}>
                      Ingresa tu contraseña actual y luego la nueva contraseña con su respectiva confirmación.
                    </Typography.Text>
                    
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                      <div>
                        <Typography.Text style={{ fontSize: 12, display: 'block', marginBottom: 4 }}>
                          Contraseña actual
                        </Typography.Text>
                        <Input.Password
                          value={passwordActual}
                          onChange={(e) => setPasswordActual(e.target.value)}
                          placeholder="Tu contraseña actual..."
                          style={{ borderRadius: 8 }}
                        />
                      </div>
                      
                      <div>
                        <Typography.Text style={{ fontSize: 12, display: 'block', marginBottom: 4 }}>
                          Nueva contraseña
                        </Typography.Text>
                        <Input.Password
                          value={nuevaPassword}
                          onChange={(e) => setNuevaPassword(e.target.value)}
                          placeholder="Mínimo 6 caracteres..."
                          style={{ borderRadius: 8 }}
                        />
                      </div>
                      
                      <div>
                        <Typography.Text style={{ fontSize: 12, display: 'block', marginBottom: 4 }}>
                          Confirmar nueva contraseña
                        </Typography.Text>
                        <Input.Password
                          value={confirmarPassword}
                          onChange={(e) => setConfirmarPassword(e.target.value)}
                          placeholder="Repite la nueva contraseña..."
                          style={{ borderRadius: 8 }}
                        />
                      </div>

                      <Button
                        type="primary"
                        onClick={handleUpdatePassword}
                        loading={savingPassword}
                        style={{
                          borderRadius: 8,
                          background: '#E30613',
                          border: 'none',
                          fontWeight: 600,
                          marginTop: 8,
                          alignSelf: 'flex-start',
                        }}
                      >
                        Actualizar contraseña
                      </Button>
                    </div>
                  </div>
                </div>
              ),
            },
          ]}
        />
      </Modal>
    </Layout>
  );
}