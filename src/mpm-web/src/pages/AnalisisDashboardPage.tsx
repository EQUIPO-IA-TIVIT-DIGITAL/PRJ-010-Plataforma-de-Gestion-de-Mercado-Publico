import { useState } from 'react'
import {
  Alert, Card, Space, Typography, Button, Spin, Descriptions,
  Empty, Row, Col, Statistic, Progress, Table, Tag, Drawer, FloatButton,
} from 'antd'
import {
  ArrowLeftOutlined, RobotOutlined,
  ReloadOutlined, TrophyOutlined, FallOutlined, RiseOutlined,
  BulbOutlined, CheckCircleOutlined, CloseCircleOutlined,
  FileTextOutlined, DollarOutlined, BarChartOutlined, AlertOutlined,
  SafetyOutlined, TeamOutlined, WarningOutlined,
} from '@ant-design/icons'
import { useNavigate, useParams } from 'react-router-dom'
import { useDashboard } from '../hooks/useAnalisis'
import { AnalisisChat, SparklesIcon } from '../components/AnalisisChat'
import { ComparativaDocumentos, type ValidacionDocumental } from '../components/ComparativaDocumentos'
import { generarPdfAnalisis } from '../lib/analisisPdf'
import { PageHeader } from '../components/PageHeader'
import { StatusBadge } from '../components/StatusBadge'
import type { StatusBadgeVariant } from '../components/StatusBadge'

interface Organismo {
  nombre?: string | null
  rut?: string | null
  unidad?: string | null
  region?: string | null
}

interface Fechas {
  publicacion?: string | null
  cierre_ofertas?: string | null
  apertura_tecnica?: string | null
  apertura_economica?: string | null
  adjudicacion?: string | null
}

interface LicitacionInfo {
  id?: string | null
  nombre?: string | null
  descripcion?: string | null
  organismo?: Organismo | null
  tipo_licitacion?: string | null
  tipo_convocatoria?: string | null
  codigo_etapa?: string | null
  estado?: string | null
  moneda?: string | null
  fechas?: Fechas | null
  monto_estimado?: number | null
  monto_estimado_moneda?: string | null
  duracion_contrato?: string | null
  renovacion?: string | null
  toma_razon_contraloria?: string | null
  prohibicion_subcontratacion?: string | null
  plazo_pago?: string | null
}

interface Adjudicatario {
  nombre?: string | null
  rut?: string | null
  monto_adjudicado?: number | null
  monto_adjudicado_moneda?: string | null
  cantidad_ofertas_recibidas?: number | null
}

interface Ofertante {
  nombre?: string | null
  rut?: string | null
  monto_ofertado?: number | null
  monto_ofertado_moneda?: string | null
  puntaje_total?: number | null
  resultado?: string | null
  motivo_inadmisibilidad?: string | null
}

interface Adjudicacion {
  adjudicatario?: Adjudicatario | null
  ofertantes?: Ofertante[]
}

interface Criterio {
  nombre?: string | null
  ponderacion?: number | null
  puntaje_maximo_total?: number | null
  puntaje_tivit_total?: number | null
  puntaje_ganador_total?: number | null
  brecha?: number | null
}

interface DesglosePuntaje {
  puntaje_tecnico?: number | null
  puntaje_economico?: number | null
  puntaje_administrativo?: number | null
  puntaje_total?: number | null
  porcentaje_cumplimiento?: number | null
}

interface Evaluacion {
  metodologia?: string | null
  criterios?: Criterio[]
  desglose_puntajes?: { tivit?: DesglosePuntaje; ganador?: DesglosePuntaje }
}

interface BrechaIdentificada {
  area?: string | null
  descripcion?: string | null
  diferencia_puntaje?: number | null
  diferencia_monto?: number | null
  impacto?: string | null
  se_puede_mitigar?: boolean | null
  recomendacion_mejora?: string | null
}

interface AnalisisTivit {
  participa?: boolean | null
  es_ganador?: boolean | null
  monto_ofertado?: number | null
  puntaje_obtenido?: number | null
  puntaje_maximo_posible?: number | null
  resultado?: string | null
  fortalezas?: string[]
  debilidades?: string[]
  brechas_identificadas?: BrechaIdentificada[]
}

interface DashboardKpi {
  indicador?: string | null
  valor?: string | null
  tendencia?: string | null
  color?: string | null
}

interface MetricasClave {
  diferencia_puntaje_total?: number | null
  diferencia_monto_ofertado?: number | null
  diferencia_porcentaje_cumplimiento?: number | null
  cantidad_ofertantes?: number | null
  ranking_tivit?: number | null
  margen_mejora_tecnico?: number | null
  margen_mejora_economico?: number | null
}

interface RiesgoIdentificado {
  riesgo?: string | null
  nivel?: string | null
  mitigacion?: string | null
  impacto_estimado?: number | null
}

// 029-fix-hallazgos-code-review-competidores-alertas (FR-012/US8, QA BUG-010): cuando el
// workspace tiene más de un documento y uno revoca/deja sin efecto a otro, GeminiService lo
// declara acá en vez de que el dashboard presente la conclusión revocada como vigente.
interface Revocacion {
  detectada?: boolean | null
  documento_que_revoca?: string | null
  documento_revocado?: string | null
  motivo?: string | null
  resultado_vigente?: string | null
}

interface AnalisisCompleto {
  licitacion?: LicitacionInfo | null
  adjudicacion?: Adjudicacion | null
  evaluacion?: Evaluacion | null
  analisis_tivit?: AnalisisTivit | null
  validacion_documental?: ValidacionDocumental
  metricas_clave?: MetricasClave | null
  dashboard_kpis?: DashboardKpi[]
  recomendaciones_estrategicas?: string[]
  riesgos_identificados?: RiesgoIdentificado[]
  revocacion?: Revocacion | null
}

function tryParse(contenido: string | null | undefined): AnalisisCompleto | null {
  if (!contenido) return null
  try {
    const parsed = JSON.parse(contenido)
    return typeof parsed === 'object' && parsed !== null ? parsed as AnalisisCompleto : null
  } catch {
    return null
  }
}

function formatMoney(value?: number | null, moneda?: string | null): string {
  if (value == null) return 'No especificado'
  const code = (moneda ?? 'CLP').toUpperCase()
  if (code === 'UF') {
    return new Intl.NumberFormat('es-CL', { maximumFractionDigits: 1, minimumFractionDigits: 1 }).format(value) + ' UF'
  }
  try {
    return new Intl.NumberFormat('es-CL', {
      style: 'currency',
      currency: code,
      maximumFractionDigits: 0,
    }).format(value)
  } catch {
    return new Intl.NumberFormat('es-CL', { maximumFractionDigits: 0 }).format(value) + ' ' + code
  }
}

function formatNumber(value?: number | null, suffix = ''): string {
  if (value == null) return '—'
  return `${value}${suffix}`
}

// US2 (spec 019): impacto -> variante de StatusBadge (antes hex propio por pantalla).
function impactoVariant(impacto?: string | null): StatusBadgeVariant {
  if (!impacto) return 'neutral'
  const i = impacto.toLowerCase()
  if (i.includes('alto')) return 'error'
  if (i.includes('medio')) return 'warning'
  if (i.includes('bajo')) return 'success'
  return 'neutral'
}

function nivelColor(nivel?: string | null): string {
  if (!nivel) return 'default'
  const n = nivel.toLowerCase()
  if (n.includes('alto')) return 'red'
  if (n.includes('medio')) return 'orange'
  if (n.includes('bajo')) return 'green'
  return 'default'
}

function tendenciaColor(tendencia?: string | null): string {
  if (tendencia?.toLowerCase() === 'negativa') return '#ef4444'
  if (tendencia?.toLowerCase() === 'positiva') return '#10b981'
  return '#3b82f6'
}

function tendenciaIcon(tendencia?: string | null) {
  if (tendencia?.toLowerCase() === 'negativa') return <FallOutlined />
  if (tendencia?.toLowerCase() === 'positiva') return <RiseOutlined />
  return null
}

// ---- Section title component ----
function SectionTitle({ icon, title, color = '#E30613' }: { icon: React.ReactNode; title: string; color?: string }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 16 }}>
      <div
        style={{
          width: 28,
          height: 28,
          borderRadius: 7,
          background: color + '18',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          color,
          fontSize: 14,
        }}
      >
        {icon}
      </div>
      <span style={{ fontWeight: 700, fontSize: 14, color: 'var(--text-primary)' }}>{title}</span>
    </div>
  )
}

export function AnalisisDashboardPage() {
  const { id } = useParams<{ id: string }>()
  const workspaceId = id ? Number(id) : null
  const navigate = useNavigate()
  const [chatOpen, setChatOpen] = useState(false)

  const { data: dashboardData, isLoading: dashboardLoading } = useDashboard(workspaceId)

  const resultado = dashboardData?.data
  const analisis = tryParse(resultado?.contenidoJson)

  if (dashboardLoading) {
    return (
      <div style={{ textAlign: 'center', padding: 80 }}>
        <Spin size="large" />
        <p style={{ marginTop: 16, color: 'var(--text-secondary)', fontSize: 14 }}>
          Cargando análisis…
        </p>
      </div>
    )
  }

  if (!resultado) {
    return (
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <Button
          icon={<ArrowLeftOutlined />}
          onClick={() => navigate(`/analisis/${workspaceId}`)}
          style={{ borderRadius: 10 }}
        >
          Volver al workspace
        </Button>
        <Card>
          <Empty description="No hay análisis disponible. Realiza un análisis primero." />
        </Card>
      </Space>
    )
  }

  if (!analisis) {
    return (
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <Button
          icon={<ArrowLeftOutlined />}
          onClick={() => navigate(`/analisis/${workspaceId}`)}
          style={{ borderRadius: 10 }}
        >
          Volver
        </Button>
        <Card>
          <Empty description="El resultado no tiene un formato JSON válido." />
        </Card>
        <Card>
          <pre style={{ whiteSpace: 'pre-wrap', maxHeight: 400, overflow: 'auto', fontSize: 12 }}>
            {resultado.contenidoJson}
          </pre>
        </Card>
      </Space>
    )
  }

  const lic = analisis.licitacion
  const adj = analisis.adjudicacion
  const ev = analisis.evaluacion
  const at = analisis.analisis_tivit
  const kpis = analisis.dashboard_kpis ?? []
  const mc = analisis.metricas_clave
  const criterios = ev?.criterios ?? []
  const ofertantes = adj?.ofertantes ?? []
  const recomendaciones = analisis.recomendaciones_estrategicas ?? []
  const riesgos = analisis.riesgos_identificados ?? []
  const moneda = lic?.moneda

  const handleDownloadPDF = () => {
    // PDF estructurado (texto real y seleccionable) generado desde el objeto
    // de datos del análisis, no desde una captura del DOM
    generarPdfAnalisis(analisis, {
      documentoNombre: resultado.documentoNombre,
      modeloUsado: resultado.modeloUsado,
      fechaAnalisis: new Date(resultado.createdAt).toLocaleString('es-CL'),
    })
  }

  return (
    <Space direction="vertical" size={20} style={{ width: '100%' }} id="dashboard-content">

      {/* ---- Page Header ---- */}
      <div>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate(`/analisis/${workspaceId}`)} type="text" style={{ marginBottom: 8, paddingLeft: 0 }}>
          Volver
        </Button>
        <PageHeader
          icon={<BarChartOutlined />}
          title={lic?.nombre ?? 'Dashboard de Análisis'}
          actions={
            <>
              <Button icon={<ReloadOutlined />} onClick={() => navigate(0)}>Recargar</Button>
              <Button icon={<FileTextOutlined />} onClick={handleDownloadPDF}>Exportar PDF</Button>
            </>
          }
        />
        <Space size={8}>
          <StatusBadge variant="success" label="Completado" icon={<CheckCircleOutlined />} />
          {lic?.id && <Typography.Text code>{lic.id}</Typography.Text>}
        </Space>
      </div>

      {/* ---- Revocación detectada (FR-012/US8) ---- */}
      {analisis?.revocacion?.detectada && (
        <Alert
          type="warning"
          showIcon
          icon={<WarningOutlined />}
          message="Se detectó una revocación entre los documentos de este workspace"
          description={
            <div>
              <p style={{ margin: 0 }}>
                <strong>{analisis.revocacion.documento_que_revoca ?? 'Un documento posterior'}</strong> deja sin efecto a{' '}
                <strong>{analisis.revocacion.documento_revocado ?? 'otro documento anterior'}</strong> de este mismo workspace.
              </p>
              {analisis.revocacion.motivo && <p style={{ margin: '4px 0 0' }}>Motivo: {analisis.revocacion.motivo}</p>}
              {analisis.revocacion.resultado_vigente && (
                <p style={{ margin: '4px 0 0' }}>
                  <strong>Resultado vigente:</strong> {analisis.revocacion.resultado_vigente}
                </p>
              )}
            </div>
          }
        />
      )}

      {/* ---- Resumen comparativo: KPIs + métricas clave en una sola sección ----
          030-qol-frontend-y-fix-scraper US6: antes "Indicadores clave" (dashboard_kpis, texto
          libre generado por Gemini) y "Métricas clave" (metricas_clave, los mismos números en
          formato numérico/Statistic) eran dos bloques separados que mostraban esencialmente la
          misma comparación TIVIT vs. ganador dos veces seguidas -- se unifican en una sola
          tarjeta: los chips de KPI arriba como resumen, el detalle numérico abajo. */}
      {(kpis.length > 0 || mc) && (
        <Card>
          <SectionTitle icon={<BarChartOutlined />} title="Resumen comparativo: TIVIT vs. ganador" color="#8b5cf6" />

          {kpis.length > 0 && (
            <Row gutter={[16, 16]} style={{ marginBottom: mc ? 24 : 0 }}>
              {kpis.map((kpi, i) => {
                const color = tendenciaColor(kpi.tendencia)
                return (
                  <Col key={i} xs={24} sm={12} md={8} lg={6}>
                    <div
                      className="mpm-stat-card"
                      style={{
                        borderTopColor: color,
                        background: 'white',
                      }}
                    >
                      <div
                        style={{
                          display: 'flex',
                          justifyContent: 'space-between',
                          alignItems: 'flex-start',
                          marginBottom: 8,
                        }}
                      >
                        <div className="mpm-stat-label">{kpi.indicador}</div>
                        {tendenciaIcon(kpi.tendencia) && (
                          <span style={{ color, fontSize: 16 }}>
                            {tendenciaIcon(kpi.tendencia)}
                          </span>
                        )}
                      </div>
                      <div className="mpm-stat-value" style={{ color }}>
                        {kpi.valor}
                      </div>
                    </div>
                  </Col>
                )
              })}
            </Row>
          )}

          {mc && <>
          <Row gutter={[24, 24]}>
            {mc.diferencia_puntaje_total != null && (
              <Col xs={24} sm={12} md={6}>
                <Statistic
                  title={<span style={{ fontSize: 12, color: 'var(--text-secondary)', fontWeight: 600 }}>Diferencia puntaje total (TIVIT vs. ganador)</span>}
                  value={mc.diferencia_puntaje_total}
                  precision={2}
                  valueStyle={{
                    color: mc.diferencia_puntaje_total < 0 ? '#ef4444' : '#10b981',
                    fontWeight: 700,
                    fontSize: 24,
                  }}
                  prefix={mc.diferencia_puntaje_total < 0 ? <FallOutlined /> : <RiseOutlined />}
                />
              </Col>
            )}
            {mc.diferencia_monto_ofertado != null && (
              <Col xs={24} sm={12} md={6}>
                <Statistic
                  title={<span style={{ fontSize: 12, color: 'var(--text-secondary)', fontWeight: 600 }}>Diferencia monto ofertado (TIVIT vs. ganador)</span>}
                  value={mc.diferencia_monto_ofertado}
                  precision={0}
                  valueStyle={{
                    color: mc.diferencia_monto_ofertado < 0 ? '#ef4444' : '#10b981',
                    fontWeight: 700,
                    fontSize: 24,
                  }}
                  prefix={mc.diferencia_monto_ofertado < 0 ? <FallOutlined /> : <RiseOutlined />}
                  formatter={(v) => formatMoney(Number(v), moneda)}
                />
              </Col>
            )}
            {ev?.desglose_puntajes?.tivit?.porcentaje_cumplimiento != null && (
              <Col xs={24} sm={12} md={6}>
                <div>
                  <p style={{ fontSize: 12, color: 'var(--text-secondary)', fontWeight: 600, marginBottom: 8 }}>
                    % Cumplimiento TIVIT
                  </p>
                  <Progress
                    percent={ev.desglose_puntajes.tivit.porcentaje_cumplimiento}
                    strokeColor={ev.desglose_puntajes.tivit.porcentaje_cumplimiento < 50 ? '#ef4444' : '#3b82f6'}
                    trailColor="#f1f5f9"
                    format={(p) => <span style={{ fontWeight: 700 }}>{p?.toFixed(1)}%</span>}
                  />
                </div>
              </Col>
            )}
            {ev?.desglose_puntajes?.ganador?.porcentaje_cumplimiento != null && (
              <Col xs={24} sm={12} md={6}>
                <div>
                  <p style={{ fontSize: 12, color: 'var(--text-secondary)', fontWeight: 600, marginBottom: 8 }}>
                    % Cumplimiento ganador
                  </p>
                  <Progress
                    percent={ev.desglose_puntajes.ganador.porcentaje_cumplimiento}
                    strokeColor="#10b981"
                    trailColor="#f1f5f9"
                    format={(p) => <span style={{ fontWeight: 700 }}>{p?.toFixed(1)}%</span>}
                  />
                </div>
              </Col>
            )}
          </Row>
          {(mc.cantidad_ofertantes != null || mc.ranking_tivit != null || mc.margen_mejora_tecnico != null || mc.margen_mejora_economico != null) && (
            <Row gutter={[24, 16]} style={{ marginTop: 8 }}>
              {mc.cantidad_ofertantes != null && (
                <Col xs={12} md={6}>
                  <Statistic title="Cantidad de ofertantes" value={mc.cantidad_ofertantes} valueStyle={{ fontSize: 18 }} />
                </Col>
              )}
              {mc.ranking_tivit != null && (
                <Col xs={12} md={6}>
                  <Statistic title="Ranking TIVIT" value={`#${mc.ranking_tivit}`} valueStyle={{ fontSize: 18 }} />
                </Col>
              )}
              {mc.margen_mejora_tecnico != null && (
                <Col xs={12} md={6}>
                  <Statistic title="Margen de mejora técnico" value={mc.margen_mejora_tecnico} precision={2} valueStyle={{ fontSize: 18 }} />
                </Col>
              )}
              {mc.margen_mejora_economico != null && (
                <Col xs={12} md={6}>
                  <Statistic title="Margen de mejora económico" value={mc.margen_mejora_economico} precision={2} valueStyle={{ fontSize: 18 }} />
                </Col>
              )}
            </Row>
          )}
          </>}
        </Card>
      )}

      {/* ---- Licitación info ---- */}
      {lic && (
        <Card>
          <SectionTitle icon={<FileTextOutlined />} title="Información de la licitación" color="#3b82f6" />
          <Descriptions column={2} size="small">
            <Descriptions.Item label="Nombre" span={2}>
              <span style={{ fontWeight: 600 }}>{lic.nombre ?? '—'}</span>
            </Descriptions.Item>
            <Descriptions.Item label="Código">
              <span style={{ fontFamily: 'monospace', fontSize: 12, color: '#3b82f6', background: '#eff6ff', padding: '2px 8px', borderRadius: 6 }}>
                {lic.id ?? '—'}
              </span>
            </Descriptions.Item>
            <Descriptions.Item label="Estado">{lic.estado ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Organismo">{lic.organismo?.nombre ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Región">{lic.organismo?.region ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Tipo de licitación">{lic.tipo_licitacion ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Tipo de convocatoria">{lic.tipo_convocatoria ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Fecha adjudicación">{lic.fechas?.adjudicacion ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Cierre de ofertas">{lic.fechas?.cierre_ofertas ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Monto estimado">
              <span style={{ fontWeight: 600, color: '#0f172a' }}>
                {formatMoney(lic.monto_estimado, lic.monto_estimado_moneda ?? moneda)}
              </span>
            </Descriptions.Item>
            <Descriptions.Item label="Duración contrato">{lic.duracion_contrato ?? '—'}</Descriptions.Item>
            {adj?.adjudicatario && (
              <Descriptions.Item label="Adjudicatario" span={2}>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <TrophyOutlined style={{ color: '#f59e0b' }} />
                    <span style={{ fontWeight: 700 }}>{adj.adjudicatario.nombre ?? '—'}</span>
                  </div>
                  <span style={{ color: 'var(--text-secondary)', fontSize: 12 }}>
                    RUT: {adj.adjudicatario.rut ?? '—'}
                  </span>
                  <span style={{ color: 'var(--text-secondary)', fontSize: 12 }}>
                    Monto adjudicado: <strong>{formatMoney(adj.adjudicatario.monto_adjudicado, adj.adjudicatario.monto_adjudicado_moneda ?? moneda)}</strong>
                  </span>
                </div>
              </Descriptions.Item>
            )}
          </Descriptions>
        </Card>
      )}

      {/* ---- Participación TIVIT ---- */}
      {at && (
        <Card>
          <SectionTitle icon={<DollarOutlined />} title="Participación de TIVIT" color="#E30613" />
          <Row gutter={[24, 16]}>
            <Col xs={24} sm={8}>
              <Statistic
                title={<span style={{ fontSize: 12, color: 'var(--text-secondary)', fontWeight: 600 }}>Monto ofertado</span>}
                value={at.monto_ofertado ?? 0}
                formatter={(v) => formatMoney(typeof v === 'number' ? v : null, moneda)}
                valueStyle={{ fontSize: 22, fontWeight: 700 }}
              />
            </Col>
            <Col xs={24} sm={8}>
              <p style={{ fontSize: 12, color: 'var(--text-secondary)', fontWeight: 600, marginBottom: 8 }}>
                Puntaje total
              </p>
              <Progress
                percent={at.puntaje_maximo_posible ? Math.round(((at.puntaje_obtenido ?? 0) / at.puntaje_maximo_posible) * 100) : 0}
                strokeColor="#ef4444"
                trailColor="#f1f5f9"
                format={() => (
                  <span style={{ fontWeight: 700, fontSize: 13 }}>
                    {formatNumber(at.puntaje_obtenido)} / {formatNumber(at.puntaje_maximo_posible)}
                  </span>
                )}
              />
            </Col>
            <Col xs={24} sm={8}>
              <p style={{ fontSize: 12, color: 'var(--text-secondary)', fontWeight: 600, marginBottom: 8 }}>
                Resultado
              </p>
              <Tag color={at.es_ganador ? 'success' : 'error'} style={{ fontSize: 13, padding: '4px 10px' }}>
                {at.resultado ?? (at.es_ganador ? 'Adjudicado' : 'No adjudicado')}
              </Tag>
            </Col>
          </Row>
        </Card>
      )}

      {/* ---- Ofertantes ---- */}
      {ofertantes.length > 0 && (
        <Card>
          <SectionTitle icon={<TeamOutlined />} title="Ofertantes" color="#3b82f6" />
          <Table
            dataSource={ofertantes.map((o, i) => ({ ...o, key: i }))}
            pagination={false}
            size="small"
            columns={[
              { title: 'Nombre', dataIndex: 'nombre', key: 'nombre', render: (v?: string | null) => v ?? '—' },
              { title: 'RUT', dataIndex: 'rut', key: 'rut', render: (v?: string | null) => v ?? '—' },
              {
                title: 'Monto ofertado', dataIndex: 'monto_ofertado', key: 'monto_ofertado',
                render: (v?: number | null, row?: Ofertante) => formatMoney(v, row?.monto_ofertado_moneda ?? moneda),
              },
              { title: 'Puntaje', dataIndex: 'puntaje_total', key: 'puntaje_total', render: (v?: number | null) => formatNumber(v) },
              {
                title: 'Resultado', dataIndex: 'resultado', key: 'resultado',
                render: (v?: string | null) => v ? <Tag color={v.toLowerCase().includes('adjudicad') && !v.toLowerCase().includes('no') ? 'success' : 'default'}>{v}</Tag> : '—',
              },
            ]}
          />
        </Card>
      )}

      {/* ---- Brechas identificadas ---- */}
      {at?.brechas_identificadas && at.brechas_identificadas.length > 0 && (
        <Card>
          <SectionTitle icon={<AlertOutlined />} title="Brechas identificadas" color="#f59e0b" />
          <Space direction="vertical" size={12} style={{ width: '100%' }}>
            {at.brechas_identificadas.map((f, i) => {
              return (
                <div
                  key={i}
                  style={{
                    background: 'var(--bg-muted)',
                    borderRadius: 10,
                    padding: '14px 16px',
                    border: '1px solid var(--border)',
                    display: 'flex',
                    gap: 14,
                    alignItems: 'flex-start',
                  }}
                >
                  <div
                    style={{
                      width: 28,
                      height: 28,
                      borderRadius: '50%',
                      background: '#0f172a',
                      color: 'white',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      fontSize: 12,
                      fontWeight: 700,
                      flexShrink: 0,
                    }}
                  >
                    {i + 1}
                  </div>
                  <div style={{ flex: 1 }}>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginBottom: 8 }}>
                      {f.area && <StatusBadge variant="neutral" label={f.area} />}
                      {f.impacto && <StatusBadge variant={impactoVariant(f.impacto)} label={`Impacto: ${f.impacto}`} />}
                      {f.diferencia_puntaje != null && (
                        <StatusBadge variant="tertiary" label={`Diferencia: ${f.diferencia_puntaje} pts`} />
                      )}
                      {f.se_puede_mitigar != null && (
                        <StatusBadge variant={f.se_puede_mitigar ? 'success' : 'error'} label={f.se_puede_mitigar ? 'Mitigable' : 'No mitigable'} />
                      )}
                    </div>
                    <Typography.Text style={{ fontSize: 13, color: 'var(--text-primary)' }}>
                      {f.descripcion}
                    </Typography.Text>
                    {f.recomendacion_mejora && (
                      <div style={{ marginTop: 6, fontSize: 12, color: 'var(--text-secondary)' }}>
                        <BulbOutlined style={{ marginRight: 4, color: '#f59e0b' }} />
                        {f.recomendacion_mejora}
                      </div>
                    )}
                  </div>
                </div>
              )
            })}
          </Space>
        </Card>
      )}

      {/* ---- Comparativa de puntajes por criterio ---- */}
      {criterios.length > 0 && (
        <Card>
          <SectionTitle icon={<BarChartOutlined />} title="Comparativa de puntajes por criterio" color="#8b5cf6" />
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
              <thead>
                <tr style={{ background: 'var(--bg-muted)' }}>
                  {['Criterio', 'Ponderación', 'TIVIT', 'Ganador', 'Máximo', 'Brecha'].map((h) => (
                    <th
                      key={h}
                      style={{
                        border: '1px solid var(--border)',
                        padding: '10px 12px',
                        textAlign: h === 'Criterio' ? 'left' : 'center',
                        fontSize: 11,
                        fontWeight: 700,
                        textTransform: 'uppercase',
                        letterSpacing: '0.05em',
                        color: 'var(--text-secondary)',
                      }}
                    >
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {criterios.map((c, i) => {
                  const brecha = c.brecha ?? ((c.puntaje_tivit_total ?? 0) - (c.puntaje_ganador_total ?? 0))
                  return (
                    <tr key={i} style={{ background: i % 2 === 0 ? 'white' : '#fafbff' }}>
                      <td style={{ border: '1px solid var(--border)', padding: '10px 12px', fontWeight: 500 }}>{c.nombre ?? '—'}</td>
                      <td style={{ border: '1px solid var(--border)', padding: '10px 12px', textAlign: 'center', color: 'var(--text-secondary)' }}>
                        {formatNumber(c.ponderacion, '%')}
                      </td>
                      <td style={{ border: '1px solid var(--border)', padding: '10px 12px', textAlign: 'center', color: '#ef4444', fontWeight: 700 }}>
                        {formatNumber(c.puntaje_tivit_total)}
                      </td>
                      <td style={{ border: '1px solid var(--border)', padding: '10px 12px', textAlign: 'center', color: '#10b981', fontWeight: 700 }}>
                        {formatNumber(c.puntaje_ganador_total)}
                      </td>
                      <td style={{ border: '1px solid var(--border)', padding: '10px 12px', textAlign: 'center', color: 'var(--text-secondary)' }}>
                        {formatNumber(c.puntaje_maximo_total)}
                      </td>
                      <td
                        style={{
                          border: '1px solid var(--border)',
                          padding: '10px 12px',
                          textAlign: 'center',
                          fontWeight: 700,
                          color: brecha < 0 ? '#ef4444' : '#10b981',
                          background: brecha < 0 ? '#fef2f2' : '#f0fdf4',
                        }}
                      >
                        {brecha > 0 ? '+' : ''}{brecha.toFixed(2)}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      {/* ---- Fortalezas y Debilidades ---- */}
      {(((at?.fortalezas?.length) ?? 0) > 0 || ((at?.debilidades?.length) ?? 0) > 0) && (
        <Row gutter={[16, 16]}>
          {at?.fortalezas && at.fortalezas.length > 0 && (
            <Col xs={24} md={12}>
              <Card style={{ height: '100%', borderColor: 'rgba(16,185,129,0.2)', borderLeftWidth: 4, borderLeftColor: '#10b981' }}>
                <SectionTitle icon={<CheckCircleOutlined />} title="Fortalezas TIVIT" color="#10b981" />
                <Space direction="vertical" size={8} style={{ width: '100%' }}>
                  {at.fortalezas.map((item, i) => (
                    <div key={i} style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
                      <CheckCircleOutlined style={{ color: '#10b981', marginTop: 3, flexShrink: 0 }} />
                      <span style={{ fontSize: 13, color: 'var(--text-primary)', lineHeight: 1.6 }}>{item}</span>
                    </div>
                  ))}
                </Space>
              </Card>
            </Col>
          )}
          {at?.debilidades && at.debilidades.length > 0 && (
            <Col xs={24} md={12}>
              <Card style={{ height: '100%', borderColor: 'rgba(239,68,68,0.2)', borderLeftWidth: 4, borderLeftColor: '#ef4444' }}>
                <SectionTitle icon={<CloseCircleOutlined />} title="Debilidades TIVIT" color="#ef4444" />
                <Space direction="vertical" size={8} style={{ width: '100%' }}>
                  {at.debilidades.map((item, i) => (
                    <div key={i} style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
                      <CloseCircleOutlined style={{ color: '#ef4444', marginTop: 3, flexShrink: 0 }} />
                      <span style={{ fontSize: 13, color: 'var(--text-primary)', lineHeight: 1.6 }}>{item}</span>
                    </div>
                  ))}
                </Space>
              </Card>
            </Col>
          )}
        </Row>
      )}

      {/* ---- Comparativa de documentos (validación documental) ---- */}
      <ComparativaDocumentos validacion={analisis.validacion_documental} />

      {/* ---- Riesgos identificados ---- */}
      {riesgos.length > 0 && (
        <Card>
          <SectionTitle icon={<SafetyOutlined />} title="Riesgos identificados" color="#ef4444" />
          <Table
            dataSource={riesgos.map((r, i) => ({ ...r, key: i }))}
            pagination={false}
            size="small"
            columns={[
              { title: 'Riesgo', dataIndex: 'riesgo', key: 'riesgo', render: (v?: string | null) => v ?? '—' },
              {
                title: 'Nivel', dataIndex: 'nivel', key: 'nivel', width: 100,
                render: (v?: string | null) => v ? <Tag icon={<WarningOutlined />} color={nivelColor(v)}>{v}</Tag> : '—',
              },
              { title: 'Mitigación', dataIndex: 'mitigacion', key: 'mitigacion', render: (v?: string | null) => v ?? '—' },
            ]}
          />
        </Card>
      )}

      {/* ---- Recomendaciones estratégicas ---- */}
      {recomendaciones.length > 0 && (
        <Card style={{ background: 'linear-gradient(135deg, #fafbff, #f0f4ff)', borderColor: 'rgba(59,130,246,0.2)' }}>
          <SectionTitle icon={<BulbOutlined />} title="Recomendaciones estratégicas" color="#3b82f6" />
          <Space direction="vertical" size={8} style={{ width: '100%' }}>
            {recomendaciones.map((item, i) => (
              <div key={i} style={{ display: 'flex', gap: 10, alignItems: 'flex-start' }}>
                <span
                  style={{
                    width: 22,
                    height: 22,
                    borderRadius: '50%',
                    background: '#eff6ff',
                    color: '#3b82f6',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontSize: 11,
                    fontWeight: 700,
                    flexShrink: 0,
                    border: '1px solid rgba(59,130,246,0.3)',
                  }}
                >
                  {i + 1}
                </span>
                <span style={{ fontSize: 13, color: 'var(--text-primary)', lineHeight: 1.6 }}>{item}</span>
              </div>
            ))}
          </Space>
        </Card>
      )}



      {/* ---- Chat contextual: botón flotante + panel lateral ---- */}
      <FloatButton
        icon={<SparklesIcon size={18} color="white" />}
        tooltip="Chat contextual con IA"
        className="mpm-chat-fab"
        style={{
          insetInlineEnd: 32,
          insetBlockEnd: 32,
        }}
        onClick={() => setChatOpen(true)}
      />
      <Drawer
        title={
          <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <SparklesIcon size={16} color="#8b5cf6" /> Chat contextual con IA
          </span>
        }
        placement="right"
        width={440}
        open={chatOpen}
        onClose={() => setChatOpen(false)}
        destroyOnClose={false}
        styles={{
          body: {
            padding: '16px 20px',
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden',
            height: '100%',
          },
        }}
      >
        <AnalisisChat workspaceId={workspaceId} maxHeight="100%" />
      </Drawer>
    </Space>
  )
}
