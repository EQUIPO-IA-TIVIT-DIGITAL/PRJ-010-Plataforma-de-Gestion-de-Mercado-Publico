import { useCallback, useRef, useEffect } from 'react'
import { Space, Typography, Table, Button, Tag, App, Alert, Empty, Popconfirm, Card, theme } from 'antd'
import {
  UploadOutlined, PlayCircleOutlined, ArrowLeftOutlined, BarChartOutlined, LoadingOutlined,
  FileTextOutlined, ClockCircleOutlined, CheckCircleOutlined, WarningOutlined,
  FolderOpenOutlined, CalendarOutlined, DeleteOutlined,
} from '@ant-design/icons'
import { useNavigate, useParams } from 'react-router-dom'
import dayjs from 'dayjs'
import { useWorkspaceDetalle, useListarDocumentos, useSubirDocumento, useAnalizar, useEliminarDocumento } from '../hooks/useAnalisis'
import type { DocumentoItem } from '../types/analisis'
import { StatusBadge } from '../components/StatusBadge'
import type { StatusBadgeVariant } from '../components/StatusBadge'

// US2 (spec 019): mismo set que AnalisisListPage.STATUS_CONFIG -- via StatusBadge.
const STATUS_CONFIG: Record<string, { variant: StatusBadgeVariant; icon: React.ReactNode; label: string }> = {
  pendiente: { variant: 'neutral', icon: <ClockCircleOutlined />, label: 'Pendiente' },
  listo: { variant: 'info', icon: <FileTextOutlined />, label: 'Listo' },
  analizando: { variant: 'warning', icon: <LoadingOutlined spin />, label: 'Analizando…' },
  completado: { variant: 'success', icon: <CheckCircleOutlined />, label: 'Completado' },
  error: { variant: 'error', icon: <WarningOutlined />, label: 'Error' },
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function InfoStat({ icon, label, value }: { icon: React.ReactNode; label: string; value: React.ReactNode }) {
  const { token } = theme.useToken()
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
      <div style={{
        width: 32, height: 32, borderRadius: token.borderRadius, background: token.colorFillTertiary,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        color: token.colorPrimary, fontSize: 14, flexShrink: 0,
      }}>
        {icon}
      </div>
      <div>
        <div style={{ fontSize: 11, color: token.colorTextTertiary, lineHeight: 1.2 }}>{label}</div>
        <div style={{ fontSize: 13, fontWeight: 600, lineHeight: 1.4 }}>{value}</div>
      </div>
    </div>
  )
}

export function AnalisisWorkspacePage() {
  const { id } = useParams<{ id: string }>()
  const workspaceId = id ? Number(id) : null
  const navigate = useNavigate()
  const { message, notification } = App.useApp()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const { token } = theme.useToken()

  const { data: workspaceData, isLoading: workspaceLoading } = useWorkspaceDetalle(workspaceId)
  const { data: docsData, isLoading: docsLoading } = useListarDocumentos(workspaceId)
  const subirMutation = useSubirDocumento()
  const analizarMutation = useAnalizar()
  const eliminarDocMutation = useEliminarDocumento()

  const workspace = workspaceData?.data
  const documentos = docsData?.data ?? []
  const isAnalizando = workspace?.estado === 'analizando'
  const cfg = STATUS_CONFIG[workspace?.estado ?? 'pendiente'] ?? STATUS_CONFIG.pendiente

  // 029-fix-hallazgos-code-review-competidores-alertas (FR-016/US12, QA BUG-004): el seguimiento
  // de la transición "analizando" → "completado"/"error" ya no vive acá -- se movió a
  // AnalisisCompletionWatcher (montado en AppLayout, sobrevive a la navegación entre páginas).

  useEffect(() => {
    if (typeof Notification !== 'undefined' && Notification.permission === 'default') {
      Notification.requestPermission().catch(() => { /* ignore */ })
    }
  }, [])

  const handleSubirArchivo = useCallback(async (file: File) => {
    if (!workspaceId) return
    try {
      await subirMutation.mutateAsync({ workspaceId, archivo: file })
      message.success('Documento subido')
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Error al subir documento')
    }
  }, [workspaceId, subirMutation, message])

  const handleAnalizar = useCallback(async (documentoId?: number) => {
    if (!workspaceId) return
    try {
      await analizarMutation.mutateAsync({ workspaceId, documentoId })
      notification.info({
        message: 'Análisis iniciado',
        description: 'Te avisaremos cuando esté listo. Puedes seguir navegando.',
        placement: 'topRight',
        duration: 4,
      })
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Error al iniciar análisis')
    }
  }, [workspaceId, analizarMutation, message, notification])

  const handleEliminarDocumento = useCallback(async (documentoId: number) => {
    if (!workspaceId) return
    try {
      await eliminarDocMutation.mutateAsync({ workspaceId, documentoId })
      message.success('Documento eliminado')
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Error al eliminar documento')
    }
  }, [workspaceId, eliminarDocMutation, message])

  const columns = [
    {
      title: 'Nombre',
      dataIndex: 'nombreArchivo',
      key: 'nombreArchivo',
      render: (nombre: string) => (
        <Space size={8}>
          <FileTextOutlined style={{ color: token.colorTextTertiary }} />
          <Typography.Text style={{ fontSize: 13 }}>{nombre}</Typography.Text>
        </Space>
      ),
    },
    {
      title: 'Tipo',
      dataIndex: 'mimeType',
      key: 'mimeType',
      width: 120,
      render: (tipo: string) => <Tag style={{ borderRadius: 6 }}>{tipo?.replace('application/', '') ?? '—'}</Tag>,
    },
    {
      title: 'Tamaño',
      dataIndex: 'tamanioBytes',
      key: 'tamanioBytes',
      width: 100,
      render: (bytes: number) => <Typography.Text type="secondary" style={{ fontSize: 12 }}>{formatBytes(bytes)}</Typography.Text>,
    },
    {
      title: 'Subido',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 160,
      render: (date: string) => <Typography.Text type="secondary" style={{ fontSize: 12 }}>{dayjs(date).format('DD-MM-YYYY HH:mm')}</Typography.Text>,
    },
    {
      title: '',
      key: 'accion',
      width: 110,
      render: (_: unknown, record: DocumentoItem) => (
        <Popconfirm
          title="¿Ocultar este documento?"
          description="Se ocultará del workspace pero permanecerá registrado en el sistema."
          onConfirm={() => handleEliminarDocumento(record.id)}
          okText="Ocultar"
          okButtonProps={{ danger: true }}
          cancelText="Cancelar"
          disabled={isAnalizando}
        >
          <Button
            type="text"
            danger
            size="small"
            icon={<DeleteOutlined />}
            disabled={isAnalizando}
            style={{ borderRadius: 8 }}
          >
            Eliminar
          </Button>
        </Popconfirm>
      ),
    },
  ]

  if (workspaceLoading) {
    return <div style={{ textAlign: 'center', padding: 60 }}><LoadingOutlined style={{ fontSize: 32 }} /></div>
  }

  return (
    <Space direction="vertical" size={20} style={{ width: '100%' }}>

      {/* ---- Page Header ---- */}
      <div>
        <Button
          icon={<ArrowLeftOutlined />}
          onClick={() => navigate('/analisis')}
          type="text"
          style={{ marginBottom: 8, paddingLeft: 0 }}
        >
          Volver a análisis
        </Button>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
            <Typography.Title level={4} style={{ margin: 0 }}>{workspace?.nombre ?? 'Workspace'}</Typography.Title>
            <StatusBadge variant={cfg.variant} label={cfg.label} icon={cfg.icon} />
          </div>
          {workspace?.estado === 'completado' && (
            <Button type="primary" icon={<BarChartOutlined />} onClick={() => navigate(`/analisis/${workspaceId}/dashboard`)}>
              Ver dashboard de resultados
            </Button>
          )}
        </div>
        {workspace?.licitacionNombre && (
          <Typography.Text type="secondary">{workspace.licitacionNombre}</Typography.Text>
        )}
      </div>

      {isAnalizando && (
        <Alert
          message="Análisis en progreso"
          description="El PDF se está procesando con Gemini 2.5 Pro. Esto puede tardar entre 20 y 90 segundos. Te avisaremos cuando esté listo."
          type="info"
          showIcon
          icon={<LoadingOutlined spin />}
          style={{ borderRadius: 12 }}
        />
      )}

      {workspace?.estado === 'error' && (
        <Alert
          type="error"
          showIcon
          icon={<WarningOutlined />}
          message="El último análisis falló"
          description="Puedes reintentar sin volver a subir los documentos."
          action={
            <Button
              type="primary"
              danger
              size="small"
              icon={<PlayCircleOutlined />}
              onClick={() => handleAnalizar()}
              disabled={!documentos.length}
              style={{ borderRadius: 8 }}
            >
              Reintentar
            </Button>
          }
          style={{ borderRadius: 12 }}
        />
      )}

      {/* ---- Info card ---- */}
      {workspace && (
        <Card styles={{ body: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 16 } }}>
          <InfoStat icon={<FolderOpenOutlined />} label="Licitación" value={workspace.licitacionNombre || '—'} />
          <InfoStat icon={<FileTextOutlined />} label="Documentos" value={workspace.documentosCount} />
          <InfoStat icon={<CalendarOutlined />} label="Creado" value={dayjs(workspace.createdAt).format('DD-MM-YYYY')} />
          <InfoStat icon={<CalendarOutlined />} label="Actualizado" value={dayjs(workspace.updatedAt).format('DD-MM-YYYY')} />
        </Card>
      )}

      {/* ---- Documentos ---- */}
      <Card
        title={<><FileTextOutlined style={{ marginRight: 8 }} />Documentos</>}
        styles={{ body: { padding: 0 } }}
        extra={
          <Space wrap>
            <Button
              type="primary"
              icon={<PlayCircleOutlined />}
              onClick={() => handleAnalizar()}
              loading={analizarMutation.isPending}
              disabled={!documentos.length || isAnalizando}
            >
              {isAnalizando ? 'Analizando...' : 'Analizar todo'}
            </Button>
            <Button
              icon={<UploadOutlined />}
              onClick={() => fileInputRef.current?.click()}
              loading={subirMutation.isPending}
              disabled={isAnalizando}
            >
              Subir documento
            </Button>
            <input
              ref={fileInputRef}
              type="file"
              accept=".pdf,application/pdf"
              hidden
              onChange={(e) => {
                const file = e.target.files?.[0]
                if (file) handleSubirArchivo(file)
                e.target.value = ''
              }}
            />
          </Space>
        }
      >
        {documentos.length === 0 && !docsLoading ? (
          <div style={{ padding: '40px 20px' }}>
            <Empty description="Sin documentos todavía — sube un PDF para empezar" />
          </div>
        ) : (
          <Table
            dataSource={documentos}
            columns={columns}
            rowKey="id"
            loading={docsLoading}
            pagination={false}
          />
        )}
      </Card>
    </Space>
  )
}
