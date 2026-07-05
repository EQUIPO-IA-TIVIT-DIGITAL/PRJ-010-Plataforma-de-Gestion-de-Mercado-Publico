import { useState } from 'react'
import { Table, Typography, Button, Empty, Spin, Tag, Popconfirm, Space } from 'antd'
import { CheckOutlined, CheckCircleOutlined, BellOutlined, StarFilled, DeleteOutlined } from '@ant-design/icons'
import {
  useNotificacionesLista, useMarcarLeida, useMarcarTodasLeidas,
  useEliminarNotificacion, useEliminarTodasNotificaciones,
} from '../hooks/useNotificaciones'
import type { NotificacionItem } from '../types/notificaciones'

const { Title, Text } = Typography

const TIPO_TAG: Record<string, string> = {
  scraper_completado: 'success',
  scraper_error: 'error',
  scraper_config_error: 'warning',
  aclaracion_detectada: 'gold',
}

const TIPO_LABEL: Record<string, string> = {
  scraper_completado: 'Scraper',
  scraper_error: 'Error',
  scraper_config_error: 'Config',
  aclaracion_detectada: 'Aclaración',
}

function parseMeta(metadata: string | null | undefined): Record<string, unknown> | null {
  if (!metadata) return null;
  try { return typeof metadata === 'string' ? JSON.parse(metadata) : metadata; }
  catch { return null; }
}

function getLicitacionUrl(metadata: string | null | undefined): string | null {
  const meta = parseMeta(metadata);
  if (!meta) return null;
  const codigo = meta.codigo_externo as string | undefined;
  return codigo ? `/licitaciones?codigo=${codigo}` : null;
}

export default function NotificacionesPage() {
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)

  const { data, isLoading } = useNotificacionesLista(page, pageSize)
  const marcarLeida = useMarcarLeida()
  const marcarTodas = useMarcarTodasLeidas()
  const eliminar = useEliminarNotificacion()
  const eliminarTodas = useEliminarTodasNotificaciones()

  const notificaciones = data?.data?.items ?? []
  const totalRecords = data?.data?.totalRecords ?? 0
  const totalPages = data?.data?.totalPages ?? 0

  const columns = [
    {
      title: '',
      dataIndex: 'leido',
      key: 'leido',
      width: 40,
      render: (leido: boolean, record: NotificacionItem) => {
        if (record.tipo === 'aclaracion_detectada') {
          return <StarFilled style={{ color: leido ? '#94a3b8' : '#f59e0b', fontSize: 16 }} />;
        }
        return leido
          ? <CheckCircleOutlined style={{ color: '#52c41a', fontSize: 16 }} />
          : <BellOutlined style={{ color: '#E30613', fontSize: 16 }} />;
      },
    },
    {
      title: 'Tipo',
      dataIndex: 'tipo',
      key: 'tipo',
      width: 100,
      render: (tipo: string) => (
        <Tag color={TIPO_TAG[tipo] || 'default'}>{TIPO_LABEL[tipo] || tipo}</Tag>
      ),
    },
    {
      title: 'Título',
      dataIndex: 'titulo',
      key: 'titulo',
      render: (titulo: string, record: NotificacionItem) => {
        const url = getLicitacionUrl(record.metadata);
        if (record.tipo === 'aclaracion_detectada' && url) {
          return (
            <a href={url} style={{ fontWeight: record.leido ? 400 : 600 }}>{titulo}</a>
          );
        }
        return <Text strong={!record.leido}>{titulo}</Text>;
      },
    },
    {
      title: 'Mensaje',
      dataIndex: 'mensaje',
      key: 'mensaje',
      render: (mensaje: string, record: NotificacionItem) => (
        <Text style={{ color: record.leido ? '#999' : '#333' }}>{mensaje}</Text>
      ),
    },
    {
      title: 'Fecha',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 180,
      render: (fecha: string) => (
        <Text style={{ fontSize: 12 }}>{new Date(fecha).toLocaleString('es-CL')}</Text>
      ),
    },
    {
      title: '',
      key: 'acciones',
      width: 50,
      render: (_: unknown, record: NotificacionItem) => (
        <Popconfirm
          title="¿Eliminar esta notificación?"
          okText="Eliminar"
          cancelText="Cancelar"
          onConfirm={(e) => { e?.stopPropagation(); eliminar.mutate(record.id) }}
          onCancel={(e) => e?.stopPropagation()}
        >
          <Button
            type="text"
            danger
            size="small"
            icon={<DeleteOutlined />}
            onClick={(e) => e.stopPropagation()}
            data-testid={`notif-delete-${record.id}`}
          />
        </Popconfirm>
      ),
    },
  ]

  return (
    <div style={{ padding: 24 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <Title level={4} style={{ margin: 0 }}>Notificaciones</Title>
        <Space>
          <Button
            icon={<CheckOutlined />}
            onClick={() => marcarTodas.mutateAsync()}
            loading={marcarTodas.isPending}
          >
            Marcar todas leídas
          </Button>
          <Popconfirm
            title="¿Borrar todas las notificaciones?"
            description="Esta acción no se puede deshacer."
            okText="Borrar todas"
            okButtonProps={{ danger: true }}
            cancelText="Cancelar"
            onConfirm={() => eliminarTodas.mutateAsync()}
          >
            <Button
              danger
              icon={<DeleteOutlined />}
              loading={eliminarTodas.isPending}
              data-testid="notif-delete-all"
            >
              Borrar todas
            </Button>
          </Popconfirm>
        </Space>
      </div>

      {isLoading ? (
        <div style={{ textAlign: 'center', padding: 48 }}><Spin size="large" /></div>
      ) : notificaciones.length === 0 ? (
        <Empty description="No hay notificaciones" />
      ) : (
        <Table
          dataSource={notificaciones}
          columns={columns}
          rowKey="id"
          pagination={{
            current: page,
            pageSize,
            total: totalRecords,
            onChange: (p) => setPage(p),
            showTotal: (total) => `Total: ${total}`,
          }}
          onRow={(record) => ({
            onClick: () => {
              if (!record.leido) marcarLeida.mutate(record.id)
            },
            style: {
              cursor: 'pointer',
              background: record.leido ? 'transparent' : '#f6f8ff',
            },
          })}
        />
      )}
    </div>
  )
}
