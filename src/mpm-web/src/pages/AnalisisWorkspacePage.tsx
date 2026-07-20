import { useState, useCallback, useRef, useEffect } from 'react'
import { Card, Space, Typography, Table, Button, Tag, App, Descriptions, Alert } from 'antd'
import { UploadOutlined, PlayCircleOutlined, ArrowLeftOutlined, BarChartOutlined, LoadingOutlined } from '@ant-design/icons'
import { useNavigate, useParams } from 'react-router-dom'
import { useWorkspaceDetalle, useListarDocumentos, useSubirDocumento, useAnalizar } from '../hooks/useAnalisis'
import type { DocumentoItem } from '../types/analisis'

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export function AnalisisWorkspacePage() {
  const { id } = useParams<{ id: string }>()
  const workspaceId = id ? Number(id) : null
  const navigate = useNavigate()
  const { message, notification } = App.useApp()
  const fileInputRef = useRef<HTMLInputElement>(null)

  const { data: workspaceData, isLoading: workspaceLoading } = useWorkspaceDetalle(workspaceId)
  const { data: docsData, isLoading: docsLoading } = useListarDocumentos(workspaceId)
  const subirMutation = useSubirDocumento()
  const analizarMutation = useAnalizar()

  const workspace = workspaceData?.data
  const documentos = docsData?.data ?? []
  const isAnalizando = workspace?.estado === 'analizando'

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

  const columns = [
    {
      title: 'Nombre',
      dataIndex: 'nombreArchivo',
      key: 'nombreArchivo',
    },
    {
      title: 'Tipo',
      dataIndex: 'mimeType',
      key: 'mimeType',
      width: 120,
    },
    {
      title: 'Tamaño',
      dataIndex: 'tamanioBytes',
      key: 'tamanioBytes',
      width: 100,
      render: (bytes: number) => formatBytes(bytes),
    },
    {
      title: 'Subido',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 180,
      render: (date: string) => new Date(date).toLocaleString('es-CL'),
    },
    {
      title: 'Acción',
      key: 'accion',
      width: 100,
      render: (_: unknown, record: DocumentoItem) => (
        <Button
          type="primary"
          size="small"
          icon={<BarChartOutlined />}
          onClick={() => handleAnalizar(record.id)}
          disabled={isAnalizando}
        >
          Analizar
        </Button>
      ),
    },
  ]

  if (workspaceLoading) {
    return <div style={{ textAlign: 'center', padding: 40 }}><LoadingOutlined style={{ fontSize: 32 }} /></div>
  }

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Space>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/analisis')}>
          Volver
        </Button>
        <Typography.Title level={3} style={{ margin: 0 }}>
          {workspace?.nombre ?? 'Workspace'}
        </Typography.Title>
        <Tag color={workspace?.estado === 'completado' ? 'success' : workspace?.estado === 'analizando' ? 'processing' : workspace?.estado === 'error' ? 'error' : 'default'}>
          {workspace?.estado ?? ''}
        </Tag>
      </Space>

      {isAnalizando && (
        <Alert
          message="Análisis en progreso"
          description="El PDF se está procesando con Gemini 2.5 Pro. Esto puede tardar entre 20 y 90 segundos. Te avisaremos cuando esté listo."
          type="info"
          showIcon
          icon={<LoadingOutlined spin />}
        />
      )}

      {workspace && (
        <Card size="small">
          <Descriptions column={2} size="small">
            <Descriptions.Item label="Licitación">{workspace.licitacionNombre}</Descriptions.Item>
            <Descriptions.Item label="Documentos">{workspace.documentosCount}</Descriptions.Item>
            <Descriptions.Item label="Creado">{new Date(workspace.createdAt).toLocaleDateString('es-CL')}</Descriptions.Item>
            <Descriptions.Item label="Actualizado">{new Date(workspace.updatedAt).toLocaleDateString('es-CL')}</Descriptions.Item>
          </Descriptions>
        </Card>
      )}

      <Card
        size="small"
        title="Documentos"
        extra={
          <Space>
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
        <Table
          dataSource={documentos}
          columns={columns}
          rowKey="id"
          loading={docsLoading}
          pagination={false}
        />
      </Card>

      {workspace?.estado === 'completado' && (
        <Card size="small">
          <Space>
            <Typography.Text>Último análisis completado:</Typography.Text>
            <Button
              type="primary"
              icon={<BarChartOutlined />}
              onClick={() => navigate(`/analisis/${workspaceId}/dashboard`)}
            >
              Ver Dashboard
            </Button>
          </Space>
        </Card>
      )}

      {workspace?.estado === 'error' && (
        <Card size="small">
          <Space direction="vertical">
            <Typography.Text strong type="danger">El último análisis falló.</Typography.Text>
            <Typography.Text type="secondary">Puedes reintentar haciendo clic en "Analizar todo".</Typography.Text>
            <Button
              type="primary"
              icon={<PlayCircleOutlined />}
              onClick={() => handleAnalizar()}
              disabled={!documentos.length}
            >
              Reintentar análisis
            </Button>
          </Space>
        </Card>
      )}
    </Space>
  )
}
