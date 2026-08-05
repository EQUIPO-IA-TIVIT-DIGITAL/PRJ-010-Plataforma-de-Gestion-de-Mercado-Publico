import { useState, useCallback } from 'react'
import {
  Space, Typography, Button, Modal, Form, Input, Select, App, Tag, Alert, Empty,
  Row, Col, Spin, Tooltip, DatePicker, Popconfirm,
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

const { RangePicker } = DatePicker

const STATUS_CONFIG: Record<string, { color: string; bg: string; icon: React.ReactNode; label: string }> = {
  pendiente: {
    color: '#64748b', bg: '#f8fafc',
    icon: <ClockCircleOutlined />, label: 'Pendiente',
  },
  listo: {
    color: '#3b82f6', bg: '#eff6ff',
    icon: <FileTextOutlined />, label: 'Listo',
  },
  analizando: {
    color: '#f59e0b', bg: '#fffbeb',
    icon: <LoadingOutlined />, label: 'Analizando…',
  },
  completado: {
    color: '#10b981', bg: '#f0fdf4',
    icon: <CheckCircleOutlined />, label: 'Completado',
  },
  error: {
    color: '#ef4444', bg: '#fef2f2',
    icon: <WarningOutlined />, label: 'Error',
  },
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
  const cfg = STATUS_CONFIG[workspace.estado] ?? STATUS_CONFIG.pendiente
  const isCompletado = workspace.estado === 'completado'

  return (
    <div
      className="mpm-workspace-card"
      style={{
        background: 'white',
        border: `1px solid ${isCompletado ? 'rgba(16,185,129,0.2)' : 'var(--border)'}`,
        borderRadius: 14,
        padding: 20,
        display: 'flex',
        flexDirection: 'column',
        gap: 14,
        cursor: 'pointer',
        transition: 'all 0.2s cubic-bezier(0.4,0,0.2,1)',
        boxShadow: 'var(--shadow-card)',
        position: 'relative',
        overflow: 'hidden',
      }}
      onClick={onOpen}
      onMouseEnter={(e) => {
        e.currentTarget.style.boxShadow = 'var(--shadow-card-hover)'
        e.currentTarget.style.transform = 'translateY(-3px)'
        e.currentTarget.style.borderColor = isCompletado
          ? 'rgba(16,185,129,0.5)'
          : '#E30613'
      }}
      onMouseLeave={(e) => {
        e.currentTarget.style.boxShadow = 'var(--shadow-card)'
        e.currentTarget.style.transform = 'translateY(0)'
        e.currentTarget.style.borderColor = isCompletado
          ? 'rgba(16,185,129,0.2)'
          : 'var(--border)'
      }}
    >
      {/* Top accent bar */}
      <div
        style={{
          position: 'absolute',
          top: 0,
          left: 0,
          right: 0,
          height: 3,
          background: isCompletado
            ? 'linear-gradient(90deg, #10b981, #34d399)'
            : 'linear-gradient(90deg, #E30613, #ff3a46)',
          borderRadius: '14px 14px 0 0',
        }}
      />

      {/* Header row */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        {/* Status badge */}
        <span
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: 5,
            padding: '4px 10px',
            borderRadius: 999,
            fontSize: 11,
            fontWeight: 600,
            color: cfg.color,
            background: cfg.bg,
          }}
        >
          {cfg.icon}
          {cfg.label}
        </span>

        {/* Delete button */}
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
            onClick={(e) => {
              e.stopPropagation()
            }}
            style={{
              opacity: 0.5,
              transition: 'opacity 0.15s',
            }}
            onMouseEnter={(e) => {
              ;(e.currentTarget as HTMLElement).style.opacity = '1'
            }}
            onMouseLeave={(e) => {
              ;(e.currentTarget as HTMLElement).style.opacity = '0.5'
            }}
          />
        </Popconfirm>
      </div>

      {/* Name */}
      <div>
        <Typography.Text
          strong
          style={{
            fontSize: 15,
            color: 'var(--text-primary)',
            display: 'block',
            lineHeight: 1.4,
            marginBottom: 4,
          }}
        >
          {workspace.nombre}
        </Typography.Text>
        {workspace.licitacionNombre && (
          <Typography.Text
            style={{
              fontSize: 12,
              color: 'var(--text-secondary)',
              display: 'block',
            }}
            ellipsis
          >
            📋 {workspace.licitacionNombre}
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
          borderTop: '1px solid var(--border)',
          marginTop: 'auto',
        }}
      >
        <Space direction="vertical" size={2}>
          <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>
            <FileTextOutlined style={{ marginRight: 4 }} />
            {workspace.documentosCount ?? 0} documento{workspace.documentosCount !== 1 ? 's' : ''}
          </span>
          <span style={{ fontSize: 11, color: 'var(--text-muted)' }} title={`Análisis generado el ${dayjs(workspace.createdAt).format('DD-MM-YYYY')}`}>
            <CalendarOutlined style={{ marginRight: 4 }} />
            {workspace.fechaAdjudicacion
              ? `Adjudicada el ${dayjs(workspace.fechaAdjudicacion).format('DD-MM-YYYY')}`
              : `Sin fecha de adjudicación · análisis del ${dayjs(workspace.createdAt).format('DD-MM-YYYY')}`}
          </span>
        </Space>
        <Button
          size="small"
          type="default"
          icon={<EyeOutlined />}
          onClick={(e) => {
            e.stopPropagation()
            onOpen()
          }}
          style={{
            borderRadius: 8,
            fontWeight: 600,
            fontSize: 12,
            ...(isCompletado
              ? {
                  background: 'linear-gradient(135deg, #10b981, #34d399)',
                  border: 'none',
                  color: 'white',
                  boxShadow: '0 2px 8px rgba(16,185,129,0.3)',
                }
              : {}),
          }}
        >
          {isCompletado ? 'Ver análisis' : 'Abrir'}
        </Button>
      </div>
    </div>
  )
}

export function AnalisisListPage() {
  const navigate = useNavigate()
  const { message } = App.useApp()
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
      <div className="mpm-page-header">
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 4 }}>
            <div
              style={{
                width: 32,
                height: 32,
                borderRadius: 8,
                background: 'linear-gradient(135deg, #8b5cf6, #a78bfa)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                boxShadow: '0 4px 10px rgba(139,92,246,0.3)',
              }}
            >
              <BarChartOutlined style={{ color: 'white', fontSize: 15 }} />
            </div>
            <h1 className="mpm-page-title">Análisis de Licitaciones</h1>
          </div>
          <p className="mpm-page-subtitle">
            Workspaces de análisis con inteligencia artificial
          </p>
        </div>

        <Button
          type="primary"
          icon={<PlusOutlined />}
          onClick={() => setModalOpen(true)}
          style={{
            height: 40,
            borderRadius: 10,
            fontWeight: 700,
            padding: '0 20px',
            background: 'linear-gradient(135deg, #E30613, #ff3a46)',
            border: 'none',
            boxShadow: '0 4px 12px rgba(227,6,19,0.3)',
          }}
        >
          Nuevo workspace
        </Button>
      </div>

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
              { value: 'pendiente', label: '⏳ Pendiente' },
              { value: 'listo', label: '📄 Listo' },
              { value: 'analizando', label: '🔄 Analizando' },
              { value: 'completado', label: '✅ Completado' },
              { value: 'error', label: '⚠ Error' },
            ]}
          />
          <RangePicker
            placeholder={['Desde', 'Hasta']}
            value={rangoFechas}
            onChange={(valores) => { setRangoFechas(valores as [Dayjs | null, Dayjs | null] | null); setPage(1) }}
            format="DD-MM-YYYY"
            style={{ borderRadius: 10 }}
          />
          {totalItems > 0 && (
            <Tag
              style={{
                padding: '6px 14px',
                borderRadius: 999,
                fontSize: 13,
                fontWeight: 600,
                background: '#faf5ff',
                border: '1px solid #ddd6fe',
                color: '#7c3aed',
              }}
            >
              {totalItems} workspace{totalItems !== 1 ? 's' : ''}
            </Tag>
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
        <div
          style={{
            background: 'white',
            borderRadius: 14,
            padding: '60px 20px',
            textAlign: 'center',
            border: '1px solid var(--border)',
            boxShadow: 'var(--shadow-card)',
          }}
        >
          <div
            style={{
              width: 64,
              height: 64,
              borderRadius: 16,
              background: '#f8fafc',
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              marginBottom: 16,
            }}
          >
            <BarChartOutlined style={{ fontSize: 28, color: '#94a3b8' }} />
          </div>
          <Typography.Title level={4} style={{ color: 'var(--text-secondary)', marginBottom: 8 }}>
            Sin workspaces
          </Typography.Title>
          <Typography.Text style={{ color: 'var(--text-muted)', fontSize: 14 }}>
            Crea tu primer workspace de análisis para comenzar
          </Typography.Text>
          <div style={{ marginTop: 20 }}>
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => setModalOpen(true)}
              style={{ borderRadius: 10, fontWeight: 600 }}
            >
              Crear workspace
            </Button>
          </div>
        </div>
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
        title={
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div
              style={{
                width: 32,
                height: 32,
                borderRadius: 8,
                background: 'linear-gradient(135deg, #8b5cf6, #a78bfa)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <PlusOutlined style={{ color: 'white' }} />
            </div>
            <span style={{ fontWeight: 700, fontSize: 15 }}>Nuevo workspace de análisis</span>
          </div>
        }
        open={modalOpen}
        onOk={handleCrear}
        onCancel={() => { setModalOpen(false); form.resetFields() }}
        confirmLoading={crearMutation.isPending}
        okText="Crear workspace"
        cancelText="Cancelar"
        okButtonProps={{
          style: {
            background: 'linear-gradient(135deg, #E30613, #ff3a46)',
            border: 'none',
            borderRadius: 10,
            fontWeight: 600,
          },
        }}
        cancelButtonProps={{ style: { borderRadius: 10 } }}
      >
        <Form form={form} layout="vertical" style={{ marginTop: 16 }}>
          <Form.Item
            name="nombre"
            label={<span style={{ fontWeight: 600, color: '#374151' }}>Nombre del workspace</span>}
            rules={[{ required: true, message: 'El nombre es requerido' }]}
          >
            <Input
              placeholder="Ej: Análisis licitación TI 2025-01"
              style={{ borderRadius: 10, height: 40 }}
              autoFocus
            />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  )
}
