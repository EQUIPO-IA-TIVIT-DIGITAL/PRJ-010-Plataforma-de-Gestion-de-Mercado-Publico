import { Input, Badge, Avatar, Typography, Spin, Empty } from 'antd';
import { UserOutlined, SearchOutlined, MessageOutlined } from '@ant-design/icons';
import type { ConversacionResumen } from '../types/mensajeria';
import dayjs from 'dayjs';

const { Text, Title } = Typography;

interface Props {
  conversaciones: ConversacionResumen[];
  selectedId: number | null;
  onSelect: (id: number) => void;
  onSearch: (value: string) => void;
  isLoading: boolean;
  presenciaMap?: Record<string, string>;
}

function getInitials(name: string): string {
  return name
    .split(' ')
    .map(n => n[0])
    .slice(0, 2)
    .join('')
    .toUpperCase();
}

function getAvatarColor(name: string): string {
  const colors = [
    '#E30613', '#3b82f6', '#10b981', '#f59e0b',
    '#8b5cf6', '#ef4444', '#06b6d4', '#84cc16',
  ];
  let hash = 0;
  for (let i = 0; i < name.length; i++) hash += name.charCodeAt(i);
  return colors[hash % colors.length];
}

export function ConversacionList({ conversaciones, selectedId, onSelect, onSearch, isLoading, presenciaMap = {} }: Props) {
  return (
    <div
      style={{
        width: 320,
        minWidth: 320,
        background: 'white',
        borderRight: '1px solid var(--border)',
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        flexShrink: 0,
      }}
    >
      {/* Header */}
      <div
        style={{
          padding: '16px 20px',
          borderBottom: '1px solid var(--border)',
          background: 'white',
          flexShrink: 0,
        }}
      >
        <Title level={5} style={{ margin: 0, fontWeight: 700, fontSize: 16, color: 'var(--text-primary)', marginBottom: 12 }}>
          <MessageOutlined style={{ marginRight: 8, color: '#E30613' }} />
          Mensajes
        </Title>
        <Input
          prefix={<SearchOutlined style={{ color: '#94a3b8' }} />}
          placeholder="Buscar conversaciones..."
          onChange={(e) => onSearch(e.target.value)}
          allowClear
          data-testid="conversacion-search"
          style={{ borderRadius: 10, background: 'var(--bg-muted)', border: '1px solid var(--border)' }}
        />
      </div>

      {/* Lista */}
      <div style={{ flex: 1, overflowY: 'auto' }}>
        {isLoading ? (
          <div style={{ textAlign: 'center', padding: 40 }}>
            <Spin />
          </div>
        ) : conversaciones.length === 0 ? (
          <div style={{ textAlign: 'center', padding: 40 }}>
            <Empty description="Sin conversaciones" image={Empty.PRESENTED_IMAGE_SIMPLE} />
          </div>
        ) : (
          conversaciones.map((conv) => {
            const isSelected = conv.id === selectedId;
            const ultimoMensaje = conv.ultimoMensaje;
            const participantes = conv.participantes.filter(p => p.userId !== 'system');
            const nombre =
              conv.tipo === 'grupal'
                ? conv.asunto || 'Chat grupal'
                : participantes[0]?.nombre || 'Usuario';
            const initials = getInitials(nombre);
            const avatarColor = getAvatarColor(nombre);

            return (
              <div
                key={conv.id}
                onClick={() => onSelect(conv.id)}
                data-testid="conversacion-item"
                style={{
                  display: 'flex',
                  gap: 12,
                  padding: '14px 20px',
                  cursor: 'pointer',
                  background: isSelected ? 'rgba(227,6,19,0.05)' : 'transparent',
                  borderLeft: isSelected ? '3px solid #E30613' : '3px solid transparent',
                  borderBottom: '1px solid var(--border)',
                  transition: 'all 0.15s ease',
                  alignItems: 'center',
                }}
                onMouseEnter={(e) => {
                  if (!isSelected) {
                    e.currentTarget.style.background = 'var(--bg-muted)';
                  }
                }}
                onMouseLeave={(e) => {
                  if (!isSelected) {
                    e.currentTarget.style.background = 'transparent';
                  }
                }}
              >
                {/* Avatar */}
                <div style={{ position: 'relative', flexShrink: 0 }}>
                  <Avatar
                    size={42}
                    style={{
                      background: avatarColor,
                      fontSize: 15,
                      fontWeight: 700,
                    }}
                    icon={initials ? undefined : <UserOutlined />}
                  >
                    {initials}
                  </Avatar>
                  {/* Presence dot */}
                  {(() => {
                    const other = conv.participantes.find(
                      p => p.userId !== (JSON.parse(localStorage.getItem('mpm_user') || '{}').userId || '')
                    );
                    const estado = other ? presenciaMap[other.userId] : null;
                    return (
                      <span
                        style={{
                          position: 'absolute',
                          bottom: 0,
                          right: 0,
                          width: 12,
                          height: 12,
                          borderRadius: '50%',
                          background: estado === 'online' ? '#22c55e' : '#94a3b8',
                          border: '2px solid white',
                        }}
                      />
                    );
                  })()}
                  {conv.noLeidos > 0 && (
                    <span
                      style={{
                        position: 'absolute',
                        top: -2,
                        right: -2,
                        background: '#E30613',
                        color: 'white',
                        borderRadius: '50%',
                        width: 18,
                        height: 18,
                        fontSize: 10,
                        fontWeight: 700,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        border: '2px solid white',
                      }}
                    >
                      {conv.noLeidos > 9 ? '9+' : conv.noLeidos}
                    </span>
                  )}
                </div>

                {/* Content */}
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', marginBottom: 3 }}>
                    <Text
                      strong
                      ellipsis
                      style={{
                        fontSize: 14,
                        fontWeight: conv.noLeidos > 0 ? 700 : 600,
                        color: 'var(--text-primary)',
                        flex: 1,
                        marginRight: 8,
                      }}
                    >
                      {nombre}
                    </Text>
                    {ultimoMensaje && (
                      <Text style={{ fontSize: 11, color: 'var(--text-muted)', whiteSpace: 'nowrap', flexShrink: 0 }}>
                        {dayjs(ultimoMensaje.createdAt).format('HH:mm')}
                      </Text>
                    )}
                  </div>
                  <Text
                    ellipsis
                    style={{
                      fontSize: 12,
                      color: conv.noLeidos > 0 ? 'var(--text-secondary)' : 'var(--text-muted)',
                      fontWeight: conv.noLeidos > 0 ? 500 : 400,
                      display: 'block',
                    }}
                  >
                    {ultimoMensaje?.contenido || 'Sin mensajes aún'}
                  </Text>
                </div>
              </div>
            );
          })
        )}
      </div>
    </div>
  );
}
