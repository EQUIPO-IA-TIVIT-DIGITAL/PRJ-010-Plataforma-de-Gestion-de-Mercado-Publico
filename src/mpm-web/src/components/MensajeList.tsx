import { Spin, Typography, Avatar, Dropdown, Button } from 'antd';
import { UserOutlined, EditOutlined, DeleteOutlined, MoreOutlined, FileOutlined, DownloadOutlined } from '@ant-design/icons';
import type { MensajeDetalle } from '../types/mensajeria';
import dayjs from 'dayjs';

const { Text } = Typography;

interface Props {
  mensajes: MensajeDetalle[];
  currentUserId: string;
  onEdit: (mensajeId: number, contenido: string) => void;
  onDelete: (mensajeId: number) => void;
  isLoading: boolean;
}

function getInitials(name: string): string {
  return name.split(' ').map(n => n[0]).slice(0, 2).join('').toUpperCase();
}

function getAvatarColor(name: string): string {
  const colors = ['#3b82f6', '#10b981', '#f59e0b', '#8b5cf6', '#ef4444', '#06b6d4'];
  let hash = 0;
  for (let i = 0; i < name.length; i++) hash += name.charCodeAt(i);
  return colors[hash % colors.length];
}

export function MensajeList({ mensajes, currentUserId, onEdit, onDelete, isLoading }: Props) {
  if (isLoading) {
    return (
      <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <Spin />
      </div>
    );
  }

  return (
    <div
      style={{
        flex: 1,
        overflowY: 'auto',
        padding: '20px',
        display: 'flex',
        flexDirection: 'column',
        gap: 4,
        background: 'var(--bg-muted)',
      }}
      data-testid="mensaje-list"
    >
      {mensajes.map((msg) => {
        const isOwn = msg.userId === currentUserId;
        const isSystem = msg.tipo === 'sistema';

        // ---- Mensaje de sistema ----
        if (isSystem) {
          return (
            <div key={msg.id} style={{ display: 'flex', justifyContent: 'center', padding: '8px 0' }}>
              <span
                style={{
                  fontSize: 12,
                  color: 'var(--text-muted)',
                  background: 'rgba(255,255,255,0.8)',
                  padding: '4px 12px',
                  borderRadius: 999,
                  border: '1px solid var(--border)',
                  fontStyle: 'italic',
                }}
              >
                {msg.contenido}
              </span>
            </div>
          );
        }

        const initials = getInitials(msg.userName || 'U');
        const avatarColor = getAvatarColor(msg.userName || 'U');

        // ---- Mensaje normal ----
        return (
          <div
            key={msg.id}
            data-testid="mensaje-bubble"
            style={{
              display: 'flex',
              flexDirection: isOwn ? 'row-reverse' : 'row',
              gap: 8,
              alignItems: 'flex-end',
              marginBottom: 8,
            }}
          >
            {/* Avatar del otro */}
            {!isOwn && (
              <Avatar
                size={32}
                style={{ background: avatarColor, fontSize: 12, fontWeight: 700, flexShrink: 0 }}
              >
                {initials}
              </Avatar>
            )}

            {/* Contenido */}
            <div style={{ maxWidth: '72%', display: 'flex', flexDirection: 'column', alignItems: isOwn ? 'flex-end' : 'flex-start' }}>
              {/* Nombre (solo si no es propio) */}
              {!isOwn && (
                <Text style={{ fontSize: 11, fontWeight: 600, color: 'var(--text-secondary)', marginBottom: 3, paddingLeft: 4 }}>
                  {msg.userName}
                </Text>
              )}

              {/* Burbuja */}
              <div
                style={{
                  position: 'relative',
                  padding: '10px 14px',
                  borderRadius: isOwn ? '18px 18px 4px 18px' : '18px 18px 18px 4px',
                  background: isOwn
                    ? 'linear-gradient(135deg, #E30613, #ff3a46)'
                    : 'white',
                  color: isOwn ? 'white' : 'var(--text-primary)',
                  boxShadow: isOwn
                    ? '0 2px 10px rgba(227,6,19,0.3)'
                    : '0 1px 3px rgba(0,0,0,0.06), 0 4px 12px rgba(0,0,0,0.04)',
                  border: isOwn ? 'none' : '1px solid var(--border)',
                  wordBreak: 'break-word',
                }}
              >
                {msg.contenido && (
                  <Text style={{ color: 'inherit', fontSize: 14, lineHeight: 1.5, display: 'block' }}>
                    {msg.contenido}
                  </Text>
                )}
                {/* Adjuntos */}
                {msg.adjuntos && msg.adjuntos.length > 0 && (
                  <div style={{ marginTop: msg.contenido ? 8 : 0, display: 'flex', flexDirection: 'column', gap: 4 }}>
                    {msg.adjuntos.map(adj => (
                      <a
                        key={adj.id}
                        href={adj.downloadUrl}
                        target="_blank"
                        rel="noopener noreferrer"
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: 8,
                          padding: '6px 10px',
                          borderRadius: 8,
                          background: isOwn ? 'rgba(255,255,255,0.15)' : 'var(--bg-muted)',
                          textDecoration: 'none',
                          color: isOwn ? 'white' : 'var(--text-primary)',
                          fontSize: 13,
                        }}
                      >
                        <FileOutlined style={{ fontSize: 18, color: isOwn ? 'rgba(255,255,255,0.8)' : '#3b82f6' }} />
                        <div style={{ flex: 1, minWidth: 0 }}>
                          <div style={{ fontWeight: 500, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                            {adj.nombreArchivo}
                          </div>
                          <div style={{ fontSize: 11, opacity: 0.7 }}>
                            {adj.tamanioBytes < 1024 * 1024
                              ? `${(adj.tamanioBytes / 1024).toFixed(0)} KB`
                              : `${(adj.tamanioBytes / (1024 * 1024)).toFixed(1)} MB`}
                          </div>
                        </div>
                        <DownloadOutlined style={{ fontSize: 14, opacity: 0.7 }} />
                      </a>
                    ))}
                  </div>
                )}
              </div>

              {/* Timestamp + menu */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 4, marginTop: 3, padding: '0 4px', flexDirection: isOwn ? 'row-reverse' : 'row' }}>
                <Text style={{ fontSize: 11, color: 'var(--text-muted)' }}>
                  {dayjs(msg.createdAt).format('HH:mm')}
                  {msg.editedAt && (
                    <span style={{ marginLeft: 4, fontStyle: 'italic' }}>(editado)</span>
                  )}
                </Text>
                {isOwn && (
                  <Dropdown
                    menu={{
                      items: [
                        {
                          key: 'edit',
                          label: 'Editar',
                          icon: <EditOutlined />,
                          onClick: () => onEdit(msg.id, msg.contenido || ''),
                        },
                        {
                          key: 'delete',
                          label: 'Eliminar',
                          icon: <DeleteOutlined />,
                          danger: true,
                          onClick: () => onDelete(msg.id),
                        },
                      ],
                    }}
                    placement="topRight"
                  >
                    <Button
                      type="text"
                      icon={<MoreOutlined />}
                      size="small"
                      style={{ color: 'var(--text-muted)', padding: '0 2px', height: 'auto', opacity: 0, transition: 'opacity 0.15s' }}
                      className="msg-menu-btn"
                    />
                  </Dropdown>
                )}
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}
