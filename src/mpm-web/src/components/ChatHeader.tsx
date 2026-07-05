import { Avatar, Typography, Badge, Dropdown, Button } from 'antd';
import { UserOutlined, TeamOutlined, MoreOutlined, InfoCircleOutlined } from '@ant-design/icons';
import type { ConversacionDetalle, PresenciaItem } from '../types/mensajeria';

const { Title, Text } = Typography;

interface Props {
  conversacion: ConversacionDetalle | null;
  presencia: PresenciaItem[];
  onParticipantesClick: () => void;
  typingUserId?: string | null;
  currentUserId?: string;
}

function getInitials(name: string): string {
  return name.split(' ').map(n => n[0]).slice(0, 2).join('').toUpperCase();
}

export function ChatHeader({ conversacion, presencia, onParticipantesClick, typingUserId, currentUserId }: Props) {
  if (!conversacion) return null;

  const participantes = conversacion.participantes.filter(p => p.userId !== 'system');
  const nombre =
    conversacion.tipo === 'grupal'
      ? conversacion.asunto || 'Chat grupal'
      : participantes[0]?.nombre || 'Usuario';

  const otroUsuario = participantes[0];
  const presenciaOtro = presencia.find(p => p.userId === otroUsuario?.userId);
  const isOnline = presenciaOtro?.estado === 'online';
  const isGrupal = conversacion.tipo === 'grupal';
  const typingName = typingUserId
    ? participantes.find(p => p.userId === typingUserId)?.nombre
    : null;

  return (
    <div
      style={{
        height: 65,
        background: 'white',
        borderBottom: '1px solid var(--border)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0 20px',
        flexShrink: 0,
        boxShadow: '0 1px 0 var(--border)',
      }}
    >
      {/* Left: Avatar + info */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{ position: 'relative' }}>
          {isGrupal ? (
            <Avatar
              size={40}
              icon={<TeamOutlined />}
              style={{ background: 'linear-gradient(135deg, #3b82f6, #6366f1)', fontSize: 18 }}
            />
          ) : (
            <Avatar
              size={40}
              style={{ background: 'linear-gradient(135deg, #E30613, #ff3a46)', fontSize: 15, fontWeight: 700 }}
            >
              {getInitials(nombre)}
            </Avatar>
          )}
          {/* Online indicator */}
          {!isGrupal && presenciaOtro && (
            <span
              style={{
                position: 'absolute',
                bottom: 1,
                right: 1,
                width: 10,
                height: 10,
                borderRadius: '50%',
                background: isOnline ? '#10b981' : '#94a3b8',
                border: '2px solid white',
              }}
            />
          )}
        </div>

        <div>
          <Title
            level={5}
            style={{ margin: 0, fontWeight: 700, fontSize: 15, color: 'var(--text-primary)', lineHeight: 1.2 }}
            data-testid="chat-header-title"
          >
            {nombre}
          </Title>
          {typingName ? (
            <Text style={{ fontSize: 12, color: '#22c55e', fontStyle: 'italic' }}>
              {typingName} está escribiendo...
            </Text>
          ) : (
            <Text style={{ fontSize: 12, color: 'var(--text-muted)' }}>
              {isGrupal
                ? `${participantes.length} participantes`
                : isOnline
                ? '🟢 En línea'
                : '⚫ Desconectado'}
            </Text>
          )}
        </div>
      </div>

      {/* Right: Actions */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
        <Button
          type="text"
          icon={<InfoCircleOutlined />}
          onClick={onParticipantesClick}
          style={{ borderRadius: 8, color: 'var(--text-secondary)', display: 'flex', alignItems: 'center', gap: 4, fontWeight: 500 }}
        >
          Participantes
        </Button>
        <Dropdown
          menu={{
            items: [
              { key: 'participantes', label: 'Ver participantes', icon: <TeamOutlined />, onClick: onParticipantesClick },
            ],
          }}
          placement="bottomRight"
        >
          <Button
            type="text"
            icon={<MoreOutlined />}
            style={{ borderRadius: 8, color: 'var(--text-secondary)' }}
          />
        </Dropdown>
      </div>
    </div>
  );
}
