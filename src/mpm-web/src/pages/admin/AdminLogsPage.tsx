import { useState } from 'react'
import { Card, Table, Tabs, Tag, Typography, Space, Select, Empty, Spin, Descriptions, Alert, Tooltip } from 'antd'
import {
  LoginOutlined, SyncOutlined, CloudDownloadOutlined, FileSearchOutlined,
  CloudServerOutlined, DashboardOutlined,
} from '@ant-design/icons'
import dayjs from 'dayjs'
import { useAdminLogs } from '../../hooks/useAdminLogs'
import { useAiProviderSettings } from '../../hooks/useSystemConfig'
import type { AdminLogItem, LogTipo } from '../../types/admin'

const { Title, Text } = Typography

const TIPO_META: Record<LogTipo, { icon: React.ReactNode; label: string }> = {
  auth: { icon: <LoginOutlined />, label: 'Inicios de sesión' },
  sync: { icon: <SyncOutlined />, label: 'Sincronizaciones' },
  scraper: { icon: <CloudDownloadOutlined />, label: 'Scraper' },
  extraccion: { icon: <FileSearchOutlined />, label: 'Extracción de documentos' },
  ai_provider: { icon: <CloudServerOutlined />, label: 'Proveedor IA' },
}

function EstadoTag({ estado }: { estado: string }) {
  const e = estado.toLowerCase()
  const ok = ['exito', 'completado', 'activo'].includes(e)
  const bad = ['fallo', 'error', 'sin_adjuntos'].includes(e) || e === 'fallo'
  const parcial = ['parcial', 'historial', 'en_progreso', 'iniciado'].includes(e)
  const color = bad ? 'error' : parcial ? 'warning' : ok ? 'success' : 'default'
  return <Tag color={color} style={{ borderRadius: 6, textTransform: 'capitalize' }}>{estado}</Tag>
}

function parseExtra(item: AdminLogItem): Record<string, unknown> | null {
  if (!item.extra) return null
  try { return JSON.parse(item.extra) } catch { return null }
}

function LogTable({ tipo, estado, limite = 100 }: { tipo: LogTipo; estado?: string | null; limite?: number }) {
  const { data, isLoading, isError } = useAdminLogs({ tipo, estado, limite })
  if (isError) return <Alert type="error" showIcon message="No se pudieron cargar los logs" />
  if (isLoading) return <div style={{ padding: 40, textAlign: 'center' }}><Spin /></div>

  const rows = data ?? []
  if (rows.length === 0) return <Empty description="Sin registros para este filtro" style={{ padding: 40 }} />

  return (
    <Table
      rowKey="id"
      size="small"
      dataSource={rows}
      pagination={{ pageSize: 15, showSizeChanger: false, showTotal: (t) => `${t} registros` }}
      columns={[
        {
          title: 'Fecha',
          dataIndex: 'fecha',
          width: 160,
          render: (v: string) => dayjs(v).format('DD/MM/YYYY HH:mm:ss'),
        },
        {
          title: 'Estado',
          dataIndex: 'estado',
          width: 130,
          render: (v: string) => <EstadoTag estado={v} />,
        },
        {
          title: 'Detalle',
          dataIndex: 'detalle',
          render: (v: string) => <span style={{ fontSize: 13 }}>{v}</span>,
        },
        {
          title: 'Detalle técnico',
          key: 'extra',
          width: 220,
          render: (_: unknown, item: AdminLogItem) => {
            const extra = parseExtra(item)
            if (!extra) return <Text type="secondary">—</Text>
            return (
              <Tooltip title={JSON.stringify(extra, null, 2)} placement="left">
                <Text code style={{ fontSize: 11, cursor: 'help' }}>{Object.keys(extra).join(', ')}</Text>
              </Tooltip>
            )
          },
        },
      ]}
    />
  )
}

function EstadoSelect({ value, onChange, options }: {
  value: string | null
  onChange: (v: string | null) => void
  options: string[]
}) {
  return (
    <Select
      allowClear
      placeholder="Todos los estados"
      value={value ?? undefined}
      onChange={(v) => onChange(v ?? null)}
      style={{ width: 180, borderRadius: 8 }}
      options={options.map((o) => ({ value: o, label: o }))}
    />
  )
}

export default function AdminLogsPage() {
  const [activeTab, setActiveTab] = useState('resumen')

  // Resumen del sistema: lo más reciente de cada origen + proveedor IA actual
  const { data: authLogs } = useAdminLogs({ tipo: 'auth', limite: 5 })
  const { data: syncLogs } = useAdminLogs({ tipo: 'sync', limite: 5 })
  const { data: scraperLogs } = useAdminLogs({ tipo: 'scraper', limite: 5 })
  const { data: extraccionLogs } = useAdminLogs({ tipo: 'extraccion', limite: 5 })
  const { data: aiProvider } = useAiProviderSettings()

  const primerRegistro = (data: AdminLogItem[] | undefined) => data?.[0]

  const ultimoLogin = primerRegistro(authLogs)
  const ultimaSync = primerRegistro(syncLogs)
  const ultimoScraper = primerRegistro(scraperLogs)
  const ultimaExtraccion = primerRegistro(extraccionLogs)
  const erroresRecientes = (authLogs ?? []).filter((l) => l.estado.toLowerCase() === 'error').length
    + (syncLogs ?? []).filter((l) => ['fallo', 'parcial'].includes(l.estado.toLowerCase())).length
    + (scraperLogs ?? []).filter((l) => l.estado.toLowerCase() === 'error').length
    + (extraccionLogs ?? []).filter((l) => l.estado.toLowerCase() === 'fallo').length

  const resumenItems = [
    { label: 'Último inicio de sesión', value: ultimoLogin ? `${ultimoLogin.detalle} · ${dayjs(ultimoLogin.fecha).format('DD/MM/YYYY HH:mm')}` : 'Sin registros' },
    { label: 'Última sincronización', value: ultimaSync ? `${ultimaSync.detalle} (${ultimaSync.estado}) · ${dayjs(ultimaSync.fecha).format('DD/MM/YYYY HH:mm')}` : 'Sin registros' },
    { label: 'Última corrida del scraper', value: ultimoScraper ? `${ultimoScraper.detalle} (${ultimoScraper.estado}) · ${dayjs(ultimoScraper.fecha).format('DD/MM/YYYY HH:mm')}` : 'Sin registros' },
    { label: 'Última extracción de documentos', value: ultimaExtraccion ? `${ultimaExtraccion.detalle} (${ultimaExtraccion.estado})` : 'Sin registros' },
    { label: 'Proveedor de IA activo', value: aiProvider?.data ? `${aiProvider.data.provider} · ${aiProvider.data.model}` : 'Consultando…' },
  ]

  return (
    <div style={{ maxWidth: 1200, margin: '0 auto' }}>
      <Title level={3} style={{ marginBottom: 4 }}>Logs y actividad del sistema</Title>
      <Text type="secondary" style={{ display: 'block', marginBottom: 16 }}>
        Todo lo que el sistema hace solo: inicios de sesión, sincronizaciones, scraper,
        extracción de documentos y cambios de proveedor de IA.
      </Text>

      <Tabs
        activeKey={activeTab}
        onChange={setActiveTab}
        items={[
          {
            key: 'resumen',
            label: <span><DashboardOutlined /> Resumen del sistema</span>,
            children: (
              <Space direction="vertical" size={16} style={{ width: '100%' }}>
                <Card title="Estado general" style={{ borderRadius: 12 }}>
                  <Descriptions
                    column={1}
                    size="small"
                    items={resumenItems.map((item) => ({
                      key: item.label,
                      label: <Text strong style={{ fontSize: 12 }}>{item.label}</Text>,
                      children: <span style={{ fontSize: 13 }}>{item.value}</span>,
                    }))}
                  />
                </Card>
                <Card title="Últimos eventos de cada origen" style={{ borderRadius: 12 }}>
                  <Space direction="vertical" size={4} style={{ width: '100%' }}>
                    {([
                      ['auth', authLogs],
                      ['sync', syncLogs],
                      ['scraper', scraperLogs],
                      ['extraccion', extraccionLogs],
                    ] as [LogTipo, AdminLogItem[] | undefined][]).map(([tipo, rows]) => (
                      <div key={tipo} style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
                        <Text strong style={{ minWidth: 200, fontSize: 12 }}>{TIPO_META[tipo].icon} {TIPO_META[tipo].label}</Text>
                        <Text type="secondary" style={{ fontSize: 12 }}>
                          {rows?.[0]
                            ? `${rows[0].detalle} · ${dayjs(rows[0].fecha).format('DD/MM/YYYY HH:mm')}`
                            : 'Sin registros'}
                        </Text>
                      </div>
                    ))}
                    <div style={{ marginTop: 8 }}>
                      <Text strong style={{ fontSize: 12 }}>Errores en los últimos eventos: </Text>
                      {erroresRecientes > 0
                        ? <Tag color="error">{erroresRecientes} evento(s) con error</Tag>
                        : <Tag color="success">Ninguno</Tag>}
                    </div>
                  </Space>
                </Card>
              </Space>
            ),
          },
          {
            key: 'auth',
            label: <span><LoginOutlined /> Inicios de sesión</span>,
            children: <LogTable tipo="auth" />,
          },
          {
            key: 'sync',
            label: <span><SyncOutlined /> Sincronizaciones</span>,
            children: <LogTable tipo="sync" />,
          },
          {
            key: 'scraper',
            label: <span><CloudDownloadOutlined /> Scraper</span>,
            children: <LogTable tipo="scraper" />,
          },
          {
            key: 'extraccion',
            label: <span><FileSearchOutlined /> Extracción</span>,
            children: <LogTable tipo="extraccion" />,
          },
          {
            key: 'ai_provider',
            label: <span><CloudServerOutlined /> Proveedor IA</span>,
            children: <LogTable tipo="ai_provider" />,
          },
        ]}
      />
    </div>
  )
}
