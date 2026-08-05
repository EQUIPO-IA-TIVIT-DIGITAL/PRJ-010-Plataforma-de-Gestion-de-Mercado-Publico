import { useState } from 'react';
import { Alert, Button, Space, Tag, Typography, Input, List, Avatar } from 'antd';
import { StarOutlined, LoadingOutlined, SendOutlined, WarningOutlined } from '@ant-design/icons';
import { useLicitacionInteres, useMarcarInteres } from '../hooks/useLicitacionesInteres';
import { useConversacionDetalle } from '../hooks/useConversacionDetalle';
import { useEnviarMensaje } from '../hooks/useEnviarMensaje';
import { useMensajes } from '../hooks/useMensajes';

const { Text } = Typography;

interface Props {
  licitacionId: number;
  licitacionNombre: string;
}

// spec 031 (US5): marcar de interés -> análisis único bajo demanda -> asignar/comentar.
// Reutiliza los paneles de asignación (conversacion_participantes) y comentarios (mensajes)
// ya existentes de Mensajería en vez de construir uno nuevo -- ver contracts/colaboracion-interes.md.
export function LicitacionInteresPanel({ licitacionId, licitacionNombre }: Props) {
  const { data: interes, isLoading } = useLicitacionInteres(licitacionId);
  const marcarInteres = useMarcarInteres();
  const { data: conversacion } = useConversacionDetalle(interes?.conversacionId ?? null);
  const { data: mensajesData } = useMensajes(interes?.conversacionId ?? null, { page: 1, pageSize: 50, before: null });
  const { enviarMensaje, isPending: enviandoMensaje } = useEnviarMensaje();
  const [comentario, setComentario] = useState('');

  if (isLoading) return <LoadingOutlined />;

  if (!interes) {
    return (
      <Button
        icon={<StarOutlined />}
        loading={marcarInteres.isPending}
        onClick={() => marcarInteres.mutate({ licitacionId, nombreLicitacion: licitacionNombre })}
        data-testid="btn-marcar-interes"
      >
        Marcar de interés
      </Button>
    );
  }

  const generando = !interes.workspaceId || !interes.conversacionId;

  return (
    <Space direction="vertical" style={{ width: '100%' }} data-testid="panel-interes">
      <Tag color="blue" icon={<StarOutlined />}>De interés desde {new Date(interes.createdAt).toLocaleDateString('es-CL')}</Tag>

      {generando && (
        <Alert
          type="info"
          showIcon
          icon={<LoadingOutlined />}
          message="Preparando análisis y espacio de discusión..."
        />
      )}

      {interes.estadoCambio && (
        <Alert
          type="warning"
          showIcon
          icon={<WarningOutlined />}
          message="El estado de esta licitación cambió desde que se marcó de interés"
          data-testid="alerta-estado-cambio"
        />
      )}

      {conversacion && (
        <>
          <Text strong>Asignados</Text>
          <Space wrap>
            {(conversacion?.participantes ?? []).map(p => (
              <Tag key={p.userId} icon={<Avatar size={14} style={{ marginRight: 4 }}>{p.nombre[0]}</Avatar>}>{p.nombre}</Tag>
            ))}
          </Space>

          <Text strong>Comentarios internos</Text>
          <List
            size="small"
            dataSource={mensajesData?.items ?? []}
            locale={{ emptyText: 'Sin comentarios todavía' }}
            renderItem={m => (
              <List.Item data-testid="mensaje-interes">
                <List.Item.Meta
                  avatar={<Avatar size="small">{m.userName[0]}</Avatar>}
                  title={<Space size={6}><Text strong>{m.userName}</Text><Text type="secondary" style={{ fontSize: 11 }}>{new Date(m.createdAt).toLocaleString('es-CL')}</Text></Space>}
                  description={m.contenido}
                />
              </List.Item>
            )}
          />
          <Space.Compact style={{ width: '100%' }}>
            <Input
              placeholder="Escribe un comentario para el equipo asignado..."
              value={comentario}
              onChange={e => setComentario(e.target.value)}
              onPressEnter={() => {
                if (!comentario.trim() || !interes.conversacionId) return;
                enviarMensaje({ conversacionId: interes.conversacionId, tipo: 'texto', contenido: comentario, replyToId: null });
                setComentario('');
              }}
              data-testid="input-comentario-interes"
            />
            <Button
              icon={<SendOutlined />}
              loading={enviandoMensaje}
              onClick={() => {
                if (!comentario.trim() || !interes.conversacionId) return;
                enviarMensaje({ conversacionId: interes.conversacionId, tipo: 'texto', contenido: comentario, replyToId: null });
                setComentario('');
              }}
            />
          </Space.Compact>
        </>
      )}
    </Space>
  );
}
