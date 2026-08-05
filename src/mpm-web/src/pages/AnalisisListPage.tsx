import { useState, useCallback } from 'react'
import {
  Space, Typography, Button, Modal, Form, Input, Select, App, Alert, Card, Empty,
  Row, Col, Spin, DatePicker, Popconfirm, theme,
} from 'antd'
import {
  PlusOutlined, DeleteOutlined, EyeOutlined, ExclamationCircleOutlined,
  BarChartOutlined, FileTextOutlined, ClockCircleOutlined, CheckCircleOutlined,
  LoadingOutlined, WarningOutlined, SearchOutlined, CalendarOutlined,
} from '@ant-design/icons'
import { useNavigate } from 'react-router-dom'
import dayjs, { type Dayjs } from 'dayjs'
import { useWorkspacesLista, useCrearWorkspace, useEliminarWorkspace } from '../hooks/useAnalisis'
import type { WorkspaceItem } from '../types/analisis'
import { PageHeader } from '../components/PageHeader'
import { StatusBadge } from '../components/StatusBadge'
import type { StatusBadgeVariant } from '../components/StatusBadge'

const { RangePicker } = DatePicker

// US2 (spec 019): reemplaza el STATUS_CONFIG con hex propio por StatusBadge (6 variantes del
// sistema) -- research.md #1.
const STATUS_CONFIG: Record<string, { variant: StatusBadgeVariant; icon: React.ReactNode; label: string }> = {
  pendiente: { variant: 'neutral', icon: <ClockCircleOutlined />, label: 'Pendiente' },
  listo: { variant: 'info', icon: <FileTextOutlined />, label: 'Listo' },
  analizando: { variant: 'warning', icon: <LoadingOutlined />, label: 'Analizando…' },
  completado: { variant: 'success', icon: <CheckCircleOutlined />, label: 'Completado' },
  error: { variant: 'error', icon: <WarningOutlined />, label: 'Error' },
}

function WorkspaceCard({
  workspace,
  onOpen,
  onDelete,
}: {
  workspace: WorkspaceItem
  onOpen: () => void
  onDelete: () => void
}) {
  const { token } = theme.useToken()
  const cfg = STATUS_CONFIG[workspace.estado] ?? STATUS_CONFIG.pendiente
  const isCompletado = workspace.estado === 'completado'

  return (
    <Card
      hoverable
      onClick={onOpen}
      className="mpm-workspace-card"
      style={{ height: '100%', borderColor: isCompletado ? token.colorSuccessBorder : undefined }}
      styles={{ body: { display: 'flex', flexDirection: 'column', gap: 14, height: '100%' } }}
    >
      {/* Header row */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <StatusBadge variant={cfg.variant} label={cfg.label} icon={cfg.icon} />

        <Popconfirm
          title="¿Eliminar este workspace?"
          description="Se ocultará de la lista y no podrás recuperarlo."
          okText="Eliminar"
          okButtonProps={{ danger: true }}
          cancelText="Cancelar"
          onConfirm={(e) => {
            e?.stopPropagation();
            onDelete();
          }}
          onCancel={(e) => e?.stopPropagation()}
        >
          <Button
            size="small"
            danger
            type="text"
            icon={<DeleteOutlined />}
            onClick={(e) => e.stopPropagation()}
          />
        </Popconfirm>
      </div>

      {/* Name */}
      <div>
        <Typography.Text strong style={{ fontSize: 15, display: 'block', lineHeight: 1.4, marginBottom: 4 }}>
          {workspace.nombre}
        </Typography.Text>
        {workspace.licitacionNombre && (
          <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block' }} ellipsis>
            <FileTextOutlined style={{ marginRight: 4 }} />
            {workspace.licitacionNombre}
          </Typography.Text>
        )}
      </div>

      {/* Footer */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          paddingTop: 10,
          borderTop: `1px solid ${token.colorBorderSecondary}`,
          marginTop: 'auto',
        }}
      >
        <Space direction="vertical" size={2}>
          <span style={{ fontSize: 12, color: token.colorTextTertiary }}>
            <FileTextOutlined style={{ marginRight: 4 }} />
            {workspace.documentosCount ?? 0} documento{workspace.documentosCount !== 1 ? 's' : ''}
          </span>
          <span style={{ fontSize: 11, color: token.colorTextTertiary }} title={`Análisis generado el ${dayjs(workspace.createdAt).format('DD-MM-YYYY')}`}>
            <CalendarOutlined style={{ marginRight: 4 }} />
            {workspace.fechaAdjudicacion
              ? `Adjudicada el ${dayjs(workspace.fechaAdjudicacion).format('DD-MM-YYYY')}`
              : `Sin fecha de adjudicación · análisis del ${dayjs(workspace.createdAt).format('DD-MM-YYYY')}`}
          </span>
        </Space>
        <Button
          size="small"
          type={isCompletado ? 'primary' : 'default'}
          icon={<EyeOutlined />}
          onClick={(e) => {
            e.stopPropagation()
            onOpen()
          }}
        >
          {isCompletado ? 'Ver análisis' : 'Abrir'}
        </Button>
      </div>
    </Card>
  )
}

export function AnalisisListPage() {
  const navigate = useNavigate()
  const { message } = App.useApp()
  const { token } = theme.useToken()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [estadoFilter, setEstadoFilter] = useState<string | undefined>()
  const [rangoFechas, setRangoFechas] = useState<[Dayjs | null, Dayjs | null] | null>(null)
  const [modalOpen, setModalOpen] = useState(false)
  const [form] = Form.useForm()

  const fechaDesde = rangoFechas?.[0]?.format('YYYY-MM-DD')
  const fechaHasta = rangoFechas?.[1]?.format('YYYY-MM-DD')

  const { data, isLoading, error, refetch } = useWorkspacesLista(page, 20, search, estadoFilter, fechaDesde, fechaHasta)
  const crearMutation = useCrearWorkspace()
  const eliminarMutation = useEliminarWorkspace()

  const workspaces = data?.data?.items ?? []
  const totalItems = data?.data?.totalRecords ?? 0

  const handleCrear = useCallback(async () => {
    const values = await form.validateFields()
    await crearMutation.mutateAsync({ nombre: values.nombre })
    message.success('Workspace creado exitosamente')
    setModalOpen(false)
    form.resetFields()
  }, [form, crearMutation, message])

  const handleEliminar = useCallback((id: number) => {
    Modal.confirm({
      title: 'Eliminar workspace',
      content: '¿Estás seguro de que deseas eliminar este workspace? Esta acción no se puede deshacer.',
      okText: 'Eliminar',
      okType: 'danger',
      cancelText: 'Cancelar',
      okButtonProps: { style: { borderRadius: 8 } },
      cancelButtonProps: { style: { borderRadius: 8 } },
      onOk: async () => {
        await eliminarMutation.mutateAsync(id)
        message.success('Workspace eliminado')
      },
    })
  }, [eliminarMutation, message])

  const handleOpen = useCallback((workspace: WorkspaceItem) => {
    navigate(`/analisis/${workspace.id}`)
  }, [navigate])

  return (
    <Space direction="vertical" size={20} style={{ width: '100%' }}>

      {/* ---- Page Header ---- */}
      <PageHeader
        icon={<BarChartOutlined />}
        title="Análisis de Licitaciones"
        subtitle="Workspaces de análisis con inteligencia artificial"
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setModalOpen(true)}>
            Nuevo workspace
          </Button>
        }
      />

      {/* ---- Filters ---- */}
      <div className="mpm-filter-bar">
        <Space wrap>
          <Input
            prefix={<SearchOutlined style={{ color: '#94a3b8' }} />}
            placeholder="Buscar workspace..."
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            onPressEnter={() => setSearch(searchInput)}
            onBlur={() => setSearch(searchInput)}
            allowClear
            onClear={() => { setSearch(''); setSearchInput('') }}
            style={{ width: 280, borderRadius: 10 }}
          />
          <Select
            placeholder="Filtrar por estado"
            allowClear
            style={{ width: 180, borderRadius: 10 }}
            onChange={(value) => setEstadoFilter(value)}
            options={[
              { value: 'pendiente', label: <><ClockCircleOutlined /> Pendiente</> },
              { value: 'listo', label: <><FileTextOutlined /> Listo</> },
              { value: 'analizando', label: <><LoadingOutlined /> Analizando</> },
              { value: 'completado', label: <><CheckCircleOutlined /> Completado</> },
              { value: 'error', label: <><WarningOutlined /> Error</> },
            ]}
          />
          <RangePicker
            placeholder={['Desde', 'Hasta']}
            value={rangoFechas}
            onChange={(valores) => { setRangoFechas(valores as [Dayjs | null, Dayjs | null] | null); setPage(1) }}
            format="DD-MM-YYYY"
          />
          {totalItems > 0 && (
            <StatusBadge variant="tertiary" label={`${totalItems} workspace${totalItems !== 1 ? 's' : ''}`} />
          )}
        </Space>
      </div>

      {/* ---- Error state ---- */}
      {error && !isLoading && (
        <Alert
          type="error"
          showIcon
          icon={<ExclamationCircleOutlined />}
          message="Error al cargar workspaces"
          description={error instanceof Error ? error.message : 'Error desconocido'}
          action={
            <Button size="small" onClick={() => refetch()} style={{ borderRadius: 8 }}>
              Reintentar
            </Button>
          }
        />
      )}

      {/* ---- Grid de workspaces ---- */}
      {isLoading ? (
        <div style={{ textAlign: 'center', padding: 60 }}>
          <Spin size="large" />
        </div>
      ) : workspaces.length === 0 ? (
        <Card>
          <Empty
            image={<BarChartOutlined style={{ fontSize: 40, color: token.colorTextTertiary }} />}
            description={
              <>
                <Typography.Title level={4} style={{ marginBottom: 4 }}>Sin workspaces</Typography.Title>
                <Typography.Text type="secondary">Crea tu primer workspace de análisis para comenzar</Typography.Text>
              </>
            }
          >
            <Button type="primary" icon={<PlusOutlined />} onClick={() => setModalOpen(true)}>
              Crear workspace
            </Button>
          </Empty>
        </Card>
      ) : (
        <Row gutter={[16, 16]}>
          {workspaces.map((ws) => (
            <Col key={ws.id} xs={24} sm={12} lg={8} xl={6}>
              <WorkspaceCard
                workspace={ws}
                onOpen={() => handleOpen(ws)}
                onDelete={() => handleEliminar(ws.id)}
              />
            </Col>
          ))}
        </Row>
      )}

      {/* ---- Pagination ---- */}
      {(data?.data?.totalPages ?? 0) > 1 && (
        <div style={{ display: 'flex', justifyContent: 'center' }}>
          <Space>
            <Button
              disabled={page <= 1}
              onClick={() => setPage(p => Math.max(1, p - 1))}
              style={{ borderRadius: 8 }}
            >
              Anterior
            </Button>
            <span style={{ color: 'var(--text-secondary)', fontSize: 13, padding: '0 8px' }}>
              Página {page} de {data?.data?.totalPages}
            </span>
            <Button
              disabled={page >= (data?.data?.totalPages ?? 1)}
              onClick={() => setPage(p => p + 1)}
              style={{ borderRadius: 8 }}
            >
              Siguiente
            </Button>
          </Space>
        </div>
      )}

      {/* ---- Modal crear workspace ---- */}
      <Modal
        title="Nuevo workspace de análisis"
        open={modalOpen}
        onOk={handleCrear}
        onCancel={() => { setModalOpen(false); form.resetFields() }}
        confirmLoading={crearMutation.isPending}
        okText="Crear workspace"
        cancelText="Cancelar"
      >
        <Form form={form} layout="vertical" style={{ marginTop: 16 }}>
          <Form.Item
            name="nombre"
            label="Nombre del workspace"
            rules={[{ required: true, message: 'El nombre es requerido' }]}
          >
            <Input placeholder="Ej: Análisis licitación TI 2025-01" autoFocus />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  )
}
