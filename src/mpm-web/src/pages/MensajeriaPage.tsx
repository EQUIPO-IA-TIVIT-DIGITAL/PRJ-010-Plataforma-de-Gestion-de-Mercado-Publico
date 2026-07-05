import { Button, Modal, Form, Input, Typography, App } from 'antd';
import { PlusOutlined, MessageOutlined } from '@ant-design/icons';
import { useState } from 'react';
import { useChatLogic } from '../hooks/useChatLogic';
import { usePresencia } from '../hooks/usePresencia';
import { ConversacionList } from '../components/ConversacionList';
import { ChatPanel } from '../components/ChatPanel';
import { CrearConversacionModal } from '../components/CrearConversacionModal';
import { ParticipantesDrawer } from '../components/ParticipantesDrawer';

export function MensajeriaPage() {
  const {
    conversaciones,
    isLoadingConversaciones,
    conversacionSeleccionada,
    mensajes,
    isLoadingMensajes,
    selectedConversacionId,
    filter,
    isCreando,
    isEnviando,
    handleSelectConversacion,
    handleCrearConversacion,
    handleEnviarMensaje,
    handleEditarMensaje,
    handleEliminarMensaje,
    handleTyping,
    setFilter,
    typingUserId,
  } = useChatLogic();

  const [modalOpen, setModalOpen] = useState(false);
  const [drawerOpen, setDrawerOpen] = useState(false);

  const participantes = conversacionSeleccionada?.participantes || [];
  const userIds = participantes.map(p => p.userId);
  const { data: presencia = [] } = usePresencia(userIds);
  const presenciaMap = Object.fromEntries(presencia.map(p => [p.userId, p.estado]));

  const currentUserId = localStorage.getItem('mpm_user')
    ? JSON.parse(localStorage.getItem('mpm_user')!).userId
    : '';

  return (
    <div
      style={{
        display: 'flex',
        height: 'calc(100vh - 64px)',
        background: 'var(--bg-muted)',
        borderRadius: 14,
        overflow: 'hidden',
        boxShadow: 'var(--shadow-card)',
        border: '1px solid var(--border)',
      }}
    >
      {/* ---- Sidebar izquierdo: lista de conversaciones ---- */}
      <div style={{ display: 'flex', flexDirection: 'column', width: 320, flexShrink: 0 }}>
        {/* Header de la sección */}
        <div
          style={{
            padding: '16px 20px',
            background: 'white',
            borderBottom: '1px solid var(--border)',
            borderRight: '1px solid var(--border)',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            flexShrink: 0,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div
              style={{
                width: 28,
                height: 28,
                borderRadius: 7,
                background: 'linear-gradient(135deg, #E30613, #ff3a46)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                boxShadow: '0 3px 8px rgba(227,6,19,0.3)',
              }}
            >
              <MessageOutlined style={{ color: 'white', fontSize: 13 }} />
            </div>
            <Typography.Text strong style={{ fontSize: 15, color: 'var(--text-primary)' }}>
              Mensajes
            </Typography.Text>
          </div>

          <Button
            type="primary"
            size="small"
            icon={<PlusOutlined />}
            onClick={() => setModalOpen(true)}
            style={{
              borderRadius: 8,
              fontWeight: 600,
              fontSize: 12,
              background: 'linear-gradient(135deg, #E30613, #ff3a46)',
              border: 'none',
              boxShadow: '0 2px 6px rgba(227,6,19,0.3)',
              height: 30,
              padding: '0 10px',
            }}
          >
            Nueva
          </Button>
        </div>

        {/* Lista de conversaciones */}
        <div style={{ flex: 1, overflowY: 'auto', borderRight: '1px solid var(--border)', background: 'white' }}>
          <ConversacionList
            conversaciones={conversaciones}
            selectedId={selectedConversacionId}
            onSelect={handleSelectConversacion}
            onSearch={(value) => setFilter({ ...filter, search: value })}
            isLoading={isLoadingConversaciones}
            presenciaMap={presenciaMap}
          />
        </div>
      </div>

      {/* ---- Panel de chat ---- */}
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
        <ChatPanel
          conversacion={conversacionSeleccionada}
          mensajes={mensajes}
          presencia={presencia}
          currentUserId={currentUserId}
          isLoadingMensajes={isLoadingMensajes}
          isEnviando={isEnviando}
          onSend={(contenido, archivos) => {
            handleEnviarMensaje(
              { tipo: archivos?.length && !contenido.trim() ? 'archivo' : 'texto', contenido, replyToId: null },
              archivos,
            );
          }}
          onEdit={(mensajeId, contenido) => handleEditarMensaje({ mensajeId, contenido })}
          onDelete={handleEliminarMensaje}
          onTyping={handleTyping}
          onParticipantesClick={() => setDrawerOpen(true)}
          typingUserId={typingUserId}
        />
      </div>

      {/* ---- Modales y drawers ---- */}
      <CrearConversacionModal
        open={modalOpen}
        onClose={() => setModalOpen(false)}
        onCreate={handleCrearConversacion}
        isPending={isCreando}
      />
      <ParticipantesDrawer
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        participantes={participantes}
      />
    </div>
  );
}
