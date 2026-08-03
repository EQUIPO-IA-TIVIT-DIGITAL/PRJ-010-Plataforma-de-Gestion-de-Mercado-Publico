import { useState } from 'react'
import {
  Card, Row, Col, Statistic, Table, Tag, Select, Spin, Empty,
  Typography, Collapse, Progress, Tooltip, Space, Tabs,
} from 'antd'
import {
  TrophyOutlined, FallOutlined, RiseOutlined, TeamOutlined,
  BarChartOutlined, DollarOutlined, AlertOutlined, BulbOutlined,
} from '@ant-design/icons'
import { useNavigate } from 'react-router-dom'
import { useEjecutivoDashboard } from '../hooks/useAnalisis'
import type { CompetidorRanking, LicitacionResumenEjecutivo } from '../types/analisis'

const { Title, Text } = Typography
const { Option } = Select

function fmt(n?: number | null, moneda = 'CLP') {
  if (n == null || n === 0) return '—'
  return new Intl.NumberFormat('es-CL', { style: 'currency', currency: moneda, maximumFractionDigits: 0 }).format(n)
}

function fmtPct(a: number, b: number) {
  if (b === 0) return '0%'
  return `${Math.round((a / b) * 100)}%`
}

export default function EjecutivoDashboardPage() {
  const navigate = useNavigate()
  const [anioFiltro, setAnioFiltro] = useState<number | null>(null)

  const { data, isLoading } = useEjecutivoDashboard(anioFiltro)
  const dash = data?.data

  if (isLoading) return (
    <div style={{ display: 'flex', justifyContent: 'center', paddingTop: 80 }}>
      <Spin size="large" tip="Cargando dashboard ejecutivo..." />
    </div>
  )

  if (!dash) return <Empty description="Sin datos disponibles" style={{ paddingTop: 80 }} />

  const winRate = dash.totalAnalizadas > 0 ? Math.round((dash.totalGanadas / dash.totalAnalizadas) * 100) : 0

  const licitacionesColumns = [
    {
      title: 'Licitación',
      dataIndex: 'nombre',
      key: 'nombre',
      render: (nombre: string, row: LicitacionResumenEjecutivo) => (
        <a onClick={() => navigate(`/analisis/${row.workspaceId}/dashboard`)} style={{ cursor: 'pointer' }}>
          {nombre}
        </a>
      ),
    },
    {
      title: 'Resultado',
      dataIndex: 'tivitGano',
      key: 'resultado',
      width: 120,
      render: (_: boolean, row: LicitacionResumenEjecutivo) => (
        row.tivitGano
          ? <Tag color="success" icon={<TrophyOutlined />}>Ganada</Tag>
          : <Tag color="error" icon={<FallOutlined />}>{row.resultadoTivit || 'Perdida'}</Tag>
      ),
    },
    {
      title: 'Adjudicatario',
      dataIndex: 'adjudicatario',
      key: 'adjudicatario',
      render: (v: string | null) => v ?? '—',
    },
    {
      title: 'Monto adjudicado',
      dataIndex: 'montoAdjudicado',
      key: 'monto',
      align: 'right' as const,
      render: (v: number | null) => fmt(v),
    },
    {
      title: 'Puntaje TIVIT',
      key: 'puntaje',
      align: 'right' as const,
      render: (_: unknown, row: LicitacionResumenEjecutivo) =>
        row.puntajeTivit != null && row.puntajeMaximo != null
          ? <Tooltip title={`Ganador: ${row.puntajeGanador?.toFixed(1) ?? '—'} / Máximo: ${row.puntajeMaximo.toFixed(1)}`}>
              <Progress
                percent={Math.round((row.puntajeTivit / row.puntajeMaximo) * 100)}
                size="small"
                strokeColor={row.tivitGano ? '#52c41a' : '#ff4d4f'}
                style={{ width: 100 }}
              />
            </Tooltip>
          : '—',
    },
    {
      title: 'Competidores',
      key: 'competidores',
      render: (_: unknown, row: LicitacionResumenEjecutivo) =>
        row.competidoresNombres.length > 0
          ? row.competidoresNombres.map(c => <Tag key={c} style={{ fontSize: 11 }}>{c}</Tag>)
          : '—',
    },
  ]

  const competidoresColumns = [
    {
      title: 'Competidor',
      dataIndex: 'nombre',
      key: 'nombre',
    },
    {
      title: 'Veces competidor',
      dataIndex: 'vecesCompetidor',
      key: 'veces',
      align: 'center' as const,
      sorter: (a: CompetidorRanking, b: CompetidorRanking) => b.vecesCompetidor - a.vecesCompetidor,
    },
    {
      title: 'Licitaciones ganadas',
      dataIndex: 'vecesGanador',
      key: 'ganadas',
      align: 'center' as const,
      render: (v: number, row: CompetidorRanking) => (
        <span>
          {v} <Text type="secondary" style={{ fontSize: 12 }}>({fmtPct(v, row.vecesCompetidor)})</Text>
        </span>
      ),
    },
    {
      title: 'Monto total adjudicado',
      dataIndex: 'montoTotalAdjudicado',
      key: 'monto',
      align: 'right' as const,
      sorter: (a: CompetidorRanking, b: CompetidorRanking) => b.montoTotalAdjudicado - a.montoTotalAdjudicado,
      render: (v: number) => fmt(v),
    },
  ]

  return (
    <div className="mpm-page-container" style={{ padding: '24px 32px', maxWidth: 1400, margin: '0 auto' }}>
      {/* Header */}
      <div className="mpm-page-header" style={{ marginBottom: 24 }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 4 }}>
            <div
              style={{
                width: 32,
                height: 32,
                borderRadius: 8,
                background: 'linear-gradient(135deg, #0f172a, #334155)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                boxShadow: '0 4px 10px rgba(15,23,42,0.3)',
              }}
            >
              <BarChartOutlined style={{ color: 'white', fontSize: 15 }} />
            </div>
            <h1 className="mpm-page-title">Dashboard Ejecutivo</h1>
          </div>
          <p className="mpm-page-subtitle">Análisis histórico de licitaciones TIVIT vs. competidores</p>
        </div>
        <Select
          placeholder="Todos los años"
          allowClear
          style={{ width: 160 }}
          value={anioFiltro ?? undefined}
          onChange={(v) => setAnioFiltro(v ?? null)}
        >
          {dash.aniosDisponibles.map(a => <Option key={a} value={a}>{a}</Option>)}
        </Select>
      </div>

      {/* KPIs */}
      <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
        <Col xs={24} sm={12} md={6}>
          <Card style={{ borderTop: '3px solid #3b82f6', height: '100%' }}>
            <Statistic
              title={<span style={{ fontSize: 12, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em' }}>Licitaciones analizadas</span>}
              value={dash.totalAnalizadas}
              prefix={<BarChartOutlined style={{ color: '#3b82f6' }} />}
              valueStyle={{ fontWeight: 700 }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card style={{ borderTop: '3px solid #52c41a', height: '100%' }}>
            <Statistic
              title={<span style={{ fontSize: 12, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em' }}>Ganadas</span>}
              value={dash.totalGanadas}
              suffix={<span style={{ fontSize: 14, color: '#94a3b8' }}>/ {dash.totalAnalizadas}</span>}
              prefix={<TrophyOutlined style={{ color: '#52c41a' }} />}
              valueStyle={{ color: '#52c41a', fontWeight: 700 }}
            />
            <Progress percent={winRate} strokeColor="#52c41a" showInfo={false} size="small" style={{ marginTop: 8 }} />
            <Text type="secondary" style={{ fontSize: 12 }}>Win rate: {winRate}%</Text>
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card style={{ borderTop: '3px solid #10b981', height: '100%' }}>
            <Statistic
              title={<span style={{ fontSize: 12, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em' }}>Monto ganado</span>}
              value={dash.montoTotalGanado}
              formatter={(v) => fmt(Number(v))}
              prefix={<RiseOutlined style={{ color: '#10b981' }} />}
              valueStyle={{ color: '#10b981', fontSize: 18, fontWeight: 700 }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card style={{ borderTop: '3px solid #ef4444', height: '100%' }}>
            <Statistic
              title={<span style={{ fontSize: 12, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em' }}>Monto perdido</span>}
              value={dash.montoTotalPerdido}
              formatter={(v) => fmt(Number(v))}
              prefix={<DollarOutlined style={{ color: '#ef4444' }} />}
              valueStyle={{ color: '#ef4444', fontSize: 18, fontWeight: 700 }}
            />
          </Card>
        </Col>
      </Row>

      {/* Puntajes promedio */}
      {(dash.puntajePromedioTivit || dash.puntajePromedioGanador) && (
        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
          <Col xs={24} md={12}>
            <Card title="Puntaje promedio TIVIT vs. Ganador" size="small">
              <Space direction="vertical" style={{ width: '100%' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <Text style={{ width: 120 }}>TIVIT</Text>
                  <Progress
                    percent={Math.round((dash.puntajePromedioTivit ?? 0))}
                    strokeColor="#3b82f6"
                    format={p => `${p?.toFixed(1)}`}
                    style={{ flex: 1 }}
                  />
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <Text style={{ width: 120 }}>Ganador prom.</Text>
                  <Progress
                    percent={Math.round((dash.puntajePromedioGanador ?? 0))}
                    strokeColor="#f59e0b"
                    format={p => `${p?.toFixed(1)}`}
                    style={{ flex: 1 }}
                  />
                </div>
              </Space>
            </Card>
          </Col>
          {dash.factoresPerdidaFrecuentes.length > 0 && (
            <Col xs={24} md={12}>
              <Card title={<><AlertOutlined /> Factores de pérdida más frecuentes</>} size="small">
                {dash.factoresPerdidaFrecuentes.map((f, i) => (
                  <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 6, alignItems: 'flex-start' }}>
                    <BulbOutlined style={{ color: '#f59e0b', marginTop: 3, flexShrink: 0 }} />
                    <Text style={{ fontSize: 13 }}>{f}</Text>
                  </div>
                ))}
              </Card>
            </Col>
          )}
        </Row>
      )}

      {/* ---- Tabs Section (Competidores vs Licitaciones) ---- */}
      <Tabs
        defaultActiveKey="1"
        style={{ marginBottom: 24 }}
        items={[
          {
            key: '1',
            label: (
              <span style={{ fontWeight: 600, fontSize: 14 }}>
                <TeamOutlined style={{ marginRight: 6 }} />
                Ranking de Competidores
              </span>
            ),
            children: (
              <Card
                bordered={false}
                extra={<Text type="secondary">{dash.rankingCompetidores.length} competidores únicos</Text>}
                style={{ borderRadius: 14, boxShadow: 'var(--shadow-card)' }}
              >
                <Collapse
                  items={dash.rankingCompetidores.map((comp, i) => ({
                    key: i,
                    label: (
                      <div style={{ display: 'flex', gap: 16, alignItems: 'center', flexWrap: 'wrap' }}>
                        <Text strong style={{ minWidth: 200 }}>{comp.nombre}</Text>
                        <Tag color="blue">{comp.vecesCompetidor}× competidor</Tag>
                        {comp.vecesGanador > 0 && (
                          <Tag color="gold">
                            <TrophyOutlined style={{ marginRight: 4 }} />
                            Ganó {comp.vecesGanador} {comp.vecesGanador === 1 ? 'vez' : 'veces'}
                          </Tag>
                        )}
                        {comp.montoTotalAdjudicado > 0 && <Tag color="green">{fmt(comp.montoTotalAdjudicado)}</Tag>}
                      </div>
                    ),
                    children: (
                      <Table
                        dataSource={comp.licitaciones}
                        columns={[
                          {
                            title: 'Licitación',
                            dataIndex: 'nombre',
                            render: (n: string, row: LicitacionResumenEjecutivo) => (
                              <a onClick={() => navigate(`/analisis/${row.workspaceId}/dashboard`)}>{n}</a>
                            ),
                          },
                          {
                            title: 'Resultado TIVIT',
                            render: (_: unknown, row: LicitacionResumenEjecutivo) =>
                              row.tivitGano
                                ? <Tag color="success">Ganó TIVIT</Tag>
                                : <Tag color="error">Perdió TIVIT</Tag>,
                            width: 130,
                          },
                          {
                            title: 'Monto adj.',
                            dataIndex: 'montoAdjudicado',
                            render: (v: number | null) => fmt(v),
                            align: 'right' as const,
                            width: 160,
                          },
                        ]}
                        rowKey="workspaceId"
                        size="small"
                        pagination={false}
                      />
                    ),
                  }))}
                />
                {dash.rankingCompetidores.length === 0 && (
                  <Empty description="No hay datos de competidores en los análisis disponibles" />
                )}
              </Card>
            )
          },
          {
            key: '2',
            label: (
              <span style={{ fontWeight: 600, fontSize: 14 }}>
                <BarChartOutlined style={{ marginRight: 6 }} />
                Todas las Licitaciones Analizadas
              </span>
            ),
            children: (
              <Card bordered={false} style={{ borderRadius: 14, boxShadow: 'var(--shadow-card)' }}>
                <Table
                  dataSource={dash.licitaciones}
                  columns={licitacionesColumns}
                  rowKey="workspaceId"
                  size="small"
                  pagination={{ pageSize: 20 }}
                />
              </Card>
            )
          }
        ]}
      />
    </div>
  )
}
