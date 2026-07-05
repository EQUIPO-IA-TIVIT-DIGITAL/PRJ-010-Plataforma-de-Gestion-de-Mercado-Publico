import { MessageOutlined } from '@ant-design/icons';
import { Typography } from 'antd';
import { ChatHeader } from './ChatHeader';
import { MensajeList } from './MensajeList';
import { MensajeInput } from './MensajeInput';
import { TypingIndicator } from './TypingIndicator';
import type { ConversacionDetalle, MensajeDetalle, PresenciaItem } from '../types/mensajeria';

interface Props {
  conversacion: ConversacionDetalle | null;
  mensajes: MensajeDetalle[];
  presencia: PresenciaItem[];
  currentUserId: string;
  isLoadingMensajes: boolean;
  isEnviando: boolean;
  onSend: (contenido: string, archivos?: File[]) => void;
  onEdit: (mensajeId: number, contenido: string) => void;
  onDelete: (mensajeId: number) => void;
  onTyping: (escribiendo: boolean) => void;
  onParticipantesClick: () => void;
  typingUserId?: string | null;
}

export function ChatPanel({
  conversacion,
  mensajes,
  presencia,
  currentUserId,
  isLoadingMensajes,
  isEnviando,
  onSend,
  onEdit,
  onDelete,
  onTyping,
  onParticipantesClick,
  typingUserId,
}: Props) {
  // ---- Empty state ----
  if (!conversacion) {
    return (
      <div
        style={{
          flex: 1,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          background: 'var(--bg-muted)',
          gap: 12,
        }}
      >
        <div
          style={{
            width: 72,
            height: 72,
            borderRadius: 20,
            background: 'white',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            boxShadow: 'var(--shadow-card)',
            border: '1px solid var(--border)',
          }}
        >
          <MessageOutlined style={{ fontSize: 32, color: '#cbd5e1' }} />
        </div>
        <Typography.Title level={4} style={{ margin: 0, color: 'var(--text-secondary)', fontWeight: 600 }}>
          Selecciona una conversación
        </Typography.Title>
        <Typography.Text style={{ color: 'var(--text-muted)', fontSize: 14 }}>
          Elige un chat de la lista o crea uno nuevo
        </Typography.Text>
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', background: 'white' }}>
      {/* Header */}
      <ChatHeader
        conversacion={conversacion}
        presencia={presencia}
        onParticipantesClick={onParticipantesClick}
        typingUserId={typingUserId}
        currentUserId={currentUserId}
      />

      {/* Mensajes */}
      <MensajeList
        mensajes={mensajes}
        currentUserId={currentUserId}
        onEdit={onEdit}
        onDelete={onDelete}
        isLoading={isLoadingMensajes}
      />

      {/* Typing indicator */}
      <TypingIndicator userName={null} />

      {/* Input */}
      <MensajeInput
        onSend={onSend}
        onTyping={onTyping}
        isPending={isEnviando}
      />
    </div>
  );
}
