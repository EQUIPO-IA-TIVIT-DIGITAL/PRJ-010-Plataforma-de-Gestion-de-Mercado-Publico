import { Button, Typography, Layout, theme } from 'antd';
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

  const { token } = theme.useToken();

  return (
    <Layout
      style={{
        height: 'calc(100vh - 112px)',
        borderRadius: token.borderRadiusLG,
        overflow: 'hidden',
        boxShadow: token.boxShadow,
        border: `1px solid ${token.colorBorderSecondary}`,
      }}
    >
      {/* ---- Sidebar izquierdo: lista de conversaciones ---- */}
      <Layout.Sider width={320} theme="light" style={{ borderRight: `1px solid ${token.colorBorderSecondary}` }}>
        <div
          style={{
            padding: '16px 20px',
            borderBottom: `1px solid ${token.colorBorderSecondary}`,
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div
              style={{
                width: 28, height: 28, borderRadius: token.borderRadiusSM,
                background: token.colorPrimary,
                display: 'flex', alignItems: 'center', justifyContent: 'center',
              }}
            >
              <MessageOutlined style={{ color: '#ffffff', fontSize: 13 }} />
            </div>
            <Typography.Text strong style={{ fontSize: 15 }}>Mensajes</Typography.Text>
          </div>

          <Button type="primary" size="small" icon={<PlusOutlined />} onClick={() => setModalOpen(true)}>
            Nueva
          </Button>
        </div>

        <div style={{ height: 'calc(100% - 65px)', overflowY: 'auto' }}>
          <ConversacionList
            conversaciones={conversaciones}
            selectedId={selectedConversacionId}
            onSelect={handleSelectConversacion}
            onSearch={(value) => setFilter({ ...filter, search: value })}
            isLoading={isLoadingConversaciones}
            presenciaMap={presenciaMap}
          />
        </div>
      </Layout.Sider>

      {/* ---- Panel de chat ---- */}
      <Layout.Content style={{ display: 'flex', flexDirection: 'column', minWidth: 0 }}>
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
      </Layout.Content>

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
    </Layout>
  );
}
