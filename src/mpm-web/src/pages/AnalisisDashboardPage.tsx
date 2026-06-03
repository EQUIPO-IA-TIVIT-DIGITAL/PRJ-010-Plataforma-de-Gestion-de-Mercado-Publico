import { useState, useCallback, useRef, useEffect } from 'react'
import {
  Card, Space, Typography, Button, Input, List, Tag, Spin, Descriptions,
  App, Divider, Empty, Row, Col, Statistic, Progress, Tooltip
} from 'antd'
import {
  ArrowLeftOutlined, SendOutlined, RobotOutlined, UserOutlined,
  ReloadOutlined, TrophyOutlined, FallOutlined, RiseOutlined,
  BulbOutlined, CheckCircleOutlined, CloseCircleOutlined,
  FileTextOutlined, DollarOutlined, BarChartOutlined, AlertOutlined
} from '@ant-design/icons'
import { useNavigate, useParams } from 'react-router-dom'
import { useDashboard, useEnviarChat, useChatHistorial } from '../hooks/useAnalisis'
import type { ChatMensaje } from '../types/analisis'

interface LicitacionInfo {
  nombre?: string
  codigo?: string
  organismo?: string
  fecha_adjudicacion?: string
  monto_estimado?: number
  moneda?: string
  adjudicatario?: {
    nombre?: string
    rut?: string
    monto_adjudicado?: number
  }
}

interface ParticipacionTivit {
  monto_ofertado?: number | null
  puntaje_total?: number
  puntaje_maximo?: number
}

interface Factor {
  categoria?: string
  descripcion?: string
  impacto?: string
  brecha?: string
}

interface ComparativaPuntaje {
  criterio?: string
  ponderacion?: number
  puntaje_tivit?: number
  puntaje_ganador?: number
  puntaje_maximo?: number
}

interface AnalisisPerdida {
  motivo_principal?: string
  factores?: Factor[]
  fortalezas_tivit?: string[]
  debilidades_tivit?: string[]
  comparativa_puntajes?: ComparativaPuntaje[]
}

interface ConclusionEjecutiva {
  resumen?: string
  lecciones_aprendidas?: string[]
  recomendaciones?: string[]
}

interface DashboardKpi {
  indicador?: string
  valor?: string
  tendencia?: string
  color?: string
}

interface MetricasClave {
  diferencia_puntaje_total?: number | null
  diferencia_monto?: number | null
  porcentaje_cumplimiento_tivit?: number | null
  porcentaje_cumplimiento_ganador?: number | null
}

interface AnalisisCompleto {
  licitacion?: LicitacionInfo
  participacion_tivit?: ParticipacionTivit
  analisis_perdida?: AnalisisPerdida
  conclusion_ejecutiva?: ConclusionEjecutiva
  dashboard_kpis?: DashboardKpi[]
  metricas_clave?: MetricasClave
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

function formatMoney(value?: number | null, moneda = 'CLP'): string {
  if (value == null) return 'No especificado'
  return new Intl.NumberFormat('es-CL', {
    style: 'currency',
    currency: moneda,
    maximumFractionDigits: 0,
  }).format(value)
}

function formatNumber(value?: number | null, suffix = ''): string {
  if (value == null) return '—'
  return `${value}${suffix}`
}

function impactoColor(impacto?: string): string {
  if (!impacto) return 'default'
  const i = impacto.toLowerCase()
  if (i.includes('alto')) return 'red'
  if (i.includes('medio')) return 'orange'
  if (i.includes('bajo')) return 'green'
  return 'default'
}

function tendenciaIcon(tendencia?: string) {
  if (tendencia?.toLowerCase() === 'negativa') return <FallOutlined />
  if (tendencia?.toLowerCase() === 'positiva') return <RiseOutlined />
  return null
}

function tendenciaColor(tendencia?: string): string {
  if (tendencia?.toLowerCase() === 'negativa') return '#ff4d4f'
  if (tendencia?.toLowerCase() === 'positiva') return '#52c41a'
  return '#1677ff'
}

export function AnalisisDashboardPage() {
  const { id } = useParams<{ id: string }>()
  const workspaceId = id ? Number(id) : null
  const navigate = useNavigate()
  const { message } = App.useApp()
  const [chatInput, setChatInput] = useState('')
  const chatEndRef = useRef<HTMLDivElement>(null)

  const { data: dashboardData, isLoading: dashboardLoading } = useDashboard(workspaceId)
  const { data: chatData, isLoading: chatLoading } = useChatHistorial(workspaceId)
  const chatMutation = useEnviarChat()

  const resultado = dashboardData?.data
  const analisis = tryParse(resultado?.contenidoJson)
  const mensajes: ChatMensaje[] = chatData?.data?.mensajes ?? []

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [mensajes])

  const handleEnviarChat = useCallback(async () => {
    if (!workspaceId || !chatInput.trim()) return
    const mensaje = chatInput.trim()
    setChatInput('')
    try {
      await chatMutation.mutateAsync({ workspaceId, mensaje })
    } catch {
      message.error('Error al enviar mensaje')
    }
  }, [workspaceId, chatInput, chatMutation, message])

  if (dashboardLoading) {
    return <div style={{ textAlign: 'center', padding: 40 }}><Spin size="large" /></div>
  }

  if (!resultado) {
    return (
      <Space direction="vertical" size="large" style={{ width: '100%' }}>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate(`/analisis/${workspaceId}`)}>
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
      <Space direction="vertical" size="large" style={{ width: '100%' }}>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate(`/analisis/${workspaceId}`)}>
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
  const part = analisis.participacion_tivit
  const ap = analisis.analisis_perdida
  const ce = analisis.conclusion_ejecutiva
  const kpis = analisis.dashboard_kpis ?? []
  const mc = analisis.metricas_clave

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Space>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate(`/analisis/${workspaceId}`)}>
          Volver
        </Button>
        <Typography.Title level={3} style={{ margin: 0 }}>
          {lic?.nombre ?? 'Dashboard de Análisis'}
        </Typography.Title>
        <Tag color="success">Completado</Tag>
      </Space>

      {kpis.length > 0 && (
        <Row gutter={[16, 16]}>
          {kpis.map((kpi, i) => (
            <Col key={i} xs={24} sm={12} md={8} lg={6}>
              <Card size="small" style={{ borderTop: `3px solid ${tendenciaColor(kpi.tendencia)}` }}>
                <Statistic
                  title={
                    <Space>
                      {tendenciaIcon(kpi.tendencia)}
                      <span>{kpi.indicador}</span>
                    </Space>
                  }
                  value={kpi.valor}
                  valueStyle={{ color: tendenciaColor(kpi.tendencia), fontSize: 22 }}
                />
              </Card>
            </Col>
          ))}
        </Row>
      )}

      {mc && (
        <Card size="small" title={<Space><BarChartOutlined /><span>Métricas clave</span></Space>}>
          <Row gutter={[16, 16]}>
            {mc.diferencia_puntaje_total != null && (
              <Col xs={24} sm={12} md={6}>
                <Statistic
                  title="Diferencia puntaje total"
                  value={mc.diferencia_puntaje_total}
                  precision={2}
                  valueStyle={{ color: mc.diferencia_puntaje_total < 0 ? '#ff4d4f' : '#52c41a' }}
                  prefix={mc.diferencia_puntaje_total < 0 ? <FallOutlined /> : <RiseOutlined />}
                />
              </Col>
            )}
            {mc.diferencia_monto != null && (
              <Col xs={24} sm={12} md={6}>
                <Statistic
                  title="Diferencia monto"
                  value={mc.diferencia_monto}
                  precision={0}
                  valueStyle={{ color: mc.diferencia_monto < 0 ? '#ff4d4f' : '#52c41a' }}
                  prefix={mc.diferencia_monto < 0 ? <FallOutlined /> : <RiseOutlined />}
                  formatter={(v) => formatMoney(Number(v), lic?.moneda ?? 'CLP')}
                />
              </Col>
            )}
            {mc.porcentaje_cumplimiento_tivit != null && (
              <Col xs={24} sm={12} md={6}>
                <div>
                  <Typography.Text type="secondary">% Cumplimiento TIVIT</Typography.Text>
                  <Progress
                    percent={mc.porcentaje_cumplimiento_tivit}
                    strokeColor={mc.porcentaje_cumplimiento_tivit < 50 ? '#ff4d4f' : '#1677ff'}
                    format={(p) => `${p?.toFixed(1)}%`}
                  />
                </div>
              </Col>
            )}
            {mc.porcentaje_cumplimiento_ganador != null && (
              <Col xs={24} sm={12} md={6}>
                <div>
                  <Typography.Text type="secondary">% Cumplimiento ganador</Typography.Text>
                  <Progress
                    percent={mc.porcentaje_cumplimiento_ganador}
                    strokeColor="#52c41a"
                    format={(p) => `${p?.toFixed(1)}%`}
                  />
                </div>
              </Col>
            )}
          </Row>
        </Card>
      )}

      {lic && (
        <Card size="small" title={<Space><FileTextOutlined /><span>Licitación</span></Space>}>
          <Descriptions column={2} size="small" bordered>
            <Descriptions.Item label="Nombre" span={2}>{lic.nombre ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Código">{lic.codigo ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Organismo">{lic.organismo ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Fecha adjudicación">{lic.fecha_adjudicacion ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Monto estimado">{formatMoney(lic.monto_estimado, lic.moneda)}</Descriptions.Item>
            {lic.adjudicatario && (
              <>
                <Descriptions.Item label="Adjudicatario" span={2}>
                  <Space direction="vertical" size={0}>
                    <Space>
                      <TrophyOutlined style={{ color: '#faad14' }} />
                      <Typography.Text strong>{lic.adjudicatario.nombre}</Typography.Text>
                    </Space>
                    <Typography.Text type="secondary">RUT: {lic.adjudicatario.rut}</Typography.Text>
                    <Typography.Text type="secondary">
                      Monto adjudicado: {formatMoney(lic.adjudicatario.monto_adjudicado, lic.moneda)}
                    </Typography.Text>
                  </Space>
                </Descriptions.Item>
              </>
            )}
          </Descriptions>
        </Card>
      )}

      {part && (
        <Card size="small" title={<Space><DollarOutlined /><span>Participación de TIVIT</span></Space>}>
          <Row gutter={[16, 16]}>
            <Col xs={24} sm={12}>
              <Statistic
                title="Monto ofertado"
                value={part.monto_ofertado}
                formatter={(v) => formatMoney(typeof v === 'number' ? v : null, lic?.moneda ?? 'CLP')}
              />
            </Col>
            <Col xs={24} sm={12}>
              <div>
                <Typography.Text type="secondary">Puntaje total</Typography.Text>
                <Progress
                  percent={part.puntaje_maximo ? (part.puntaje_total ?? 0) : 0}
                  strokeColor="#ff4d4f"
                  format={() => `${formatNumber(part.puntaje_total)} / ${formatNumber(part.puntaje_maximo)}`}
                />
              </div>
            </Col>
          </Row>
        </Card>
      )}

      {ap?.motivo_principal && (
        <Card size="small" title={<Space><AlertOutlined style={{ color: '#ff4d4f' }} /><span>Motivo principal de la pérdida</span></Space>}>
          <Typography.Paragraph style={{ whiteSpace: 'pre-wrap', margin: 0 }}>
            {ap.motivo_principal}
          </Typography.Paragraph>
        </Card>
      )}

      {ap?.factores && ap.factores.length > 0 && (
        <Card size="small" title="Factores de pérdida">
          <List
            size="small"
            dataSource={ap.factores}
            renderItem={(f, i) => (
              <List.Item>
                <Space direction="vertical" size={4} style={{ width: '100%' }}>
                  <Space wrap>
                    <Tag color="blue">{i + 1}</Tag>
                    <Tag>{f.categoria}</Tag>
                    <Tag color={impactoColor(f.impacto)}>Impacto: {f.impacto}</Tag>
                    {f.brecha && <Tag color="purple">Brecha: {f.brecha}</Tag>}
                  </Space>
                  <Typography.Text>{f.descripcion}</Typography.Text>
                </Space>
              </List.Item>
            )}
          />
        </Card>
      )}

      {ap?.comparativa_puntajes && ap.comparativa_puntajes.length > 0 && (
        <Card size="small" title="Comparativa de puntajes">
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr style={{ background: '#fafafa' }}>
                  <th style={{ border: '1px solid #d9d9d9', padding: 8, textAlign: 'left' }}>Criterio</th>
                  <th style={{ border: '1px solid #d9d9d9', padding: 8, textAlign: 'center' }}>Ponderación</th>
                  <th style={{ border: '1px solid #d9d9d9', padding: 8, textAlign: 'center' }}>TIVIT</th>
                  <th style={{ border: '1px solid #d9d9d9', padding: 8, textAlign: 'center' }}>Ganador</th>
                  <th style={{ border: '1px solid #d9d9d9', padding: 8, textAlign: 'center' }}>Máximo</th>
                  <th style={{ border: '1px solid #d9d9d9', padding: 8, textAlign: 'center' }}>Brecha</th>
                </tr>
              </thead>
              <tbody>
                {ap.comparativa_puntajes.map((c, i) => {
                  const brecha = (c.puntaje_tivit ?? 0) - (c.puntaje_ganador ?? 0)
                  return (
                    <tr key={i}>
                      <td style={{ border: '1px solid #d9d9d9', padding: 8 }}>{c.criterio}</td>
                      <td style={{ border: '1px solid #d9d9d9', padding: 8, textAlign: 'center' }}>{formatNumber(c.ponderacion, '%')}</td>
                      <td style={{ border: '1px solid #d9d9d9', padding: 8, textAlign: 'center', color: '#ff4d4f', fontWeight: 600 }}>{formatNumber(c.puntaje_tivit)}</td>
                      <td style={{ border: '1px solid #d9d9d9', padding: 8, textAlign: 'center', color: '#52c41a', fontWeight: 600 }}>{formatNumber(c.puntaje_ganador)}</td>
                      <td style={{ border: '1px solid #d9d9d9', padding: 8, textAlign: 'center' }}>{formatNumber(c.puntaje_maximo)}</td>
                      <td style={{ border: '1px solid #d9d9d9', padding: 8, textAlign: 'center', color: brecha < 0 ? '#ff4d4f' : '#52c41a' }}>
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

      {((ap?.fortalezas_tivit && ap.fortalezas_tivit.length > 0) || (ap?.debilidades_tivit && ap.debilidades_tivit.length > 0)) && (
        <Row gutter={[16, 16]}>
          {ap?.fortalezas_tivit && ap.fortalezas_tivit.length > 0 && (
            <Col xs={24} md={12}>
              <Card size="small" title={<Space><CheckCircleOutlined style={{ color: '#52c41a' }} /><span>Fortalezas TIVIT</span></Space>}>
                <List
                  size="small"
                  dataSource={ap.fortalezas_tivit}
                  renderItem={(item, i) => (
                    <List.Item>
                      <Space align="start">
                        <CheckCircleOutlined style={{ color: '#52c41a', marginTop: 4 }} />
                        <span>{item}</span>
                      </Space>
                    </List.Item>
                  )}
                />
              </Card>
            </Col>
          )}
          {ap?.debilidades_tivit && ap.debilidades_tivit.length > 0 && (
            <Col xs={24} md={12}>
              <Card size="small" title={<Space><CloseCircleOutlined style={{ color: '#ff4d4f' }} /><span>Debilidades TIVIT</span></Space>}>
                <List
                  size="small"
                  dataSource={ap.debilidades_tivit}
                  renderItem={(item, i) => (
                    <List.Item>
                      <Space align="start">
                        <CloseCircleOutlined style={{ color: '#ff4d4f', marginTop: 4 }} />
                        <span>{item}</span>
                      </Space>
                    </List.Item>
                  )}
                />
              </Card>
            </Col>
          )}
        </Row>
      )}

      {ce?.resumen && (
        <Card size="small" title="Resumen ejecutivo">
          <Typography.Paragraph style={{ whiteSpace: 'pre-wrap', margin: 0 }}>
            {ce.resumen}
          </Typography.Paragraph>
        </Card>
      )}

      {ce?.lecciones_aprendidas && ce.lecciones_aprendidas.length > 0 && (
        <Card size="small" title={<Space><BulbOutlined style={{ color: '#faad14' }} /><span>Lecciones aprendidas</span></Space>}>
          <List
            size="small"
            dataSource={ce.lecciones_aprendidas}
            renderItem={(item, i) => (
              <List.Item>
                <Space align="start">
                  <Tag color="gold">{i + 1}</Tag>
                  <span>{item}</span>
                </Space>
              </List.Item>
            )}
          />
        </Card>
      )}

      {ce?.recomendaciones && ce.recomendaciones.length > 0 && (
        <Card size="small" title="Recomendaciones">
          <List
            size="small"
            dataSource={ce.recomendaciones}
            renderItem={(item, i) => (
              <List.Item>
                <Space align="start">
                  <Tag color="blue">{i + 1}</Tag>
                  <span>{item}</span>
                </Space>
              </List.Item>
            )}
          />
        </Card>
      )}

      <Card size="small" title="Metadata" extra={<Button icon={<ReloadOutlined />} onClick={() => navigate(0)} size="small">Recargar</Button>}>
        <Descriptions column={2} size="small">
          <Descriptions.Item label="Documento">{resultado.documentoNombre}</Descriptions.Item>
          <Descriptions.Item label="Modelo">{resultado.modeloUsado}</Descriptions.Item>
          <Descriptions.Item label="Tokens entrada">{resultado.tokensEntrada.toLocaleString()}</Descriptions.Item>
          <Descriptions.Item label="Tokens salida">{resultado.tokensSalida.toLocaleString()}</Descriptions.Item>
          <Descriptions.Item label="Fecha">{new Date(resultado.createdAt).toLocaleString('es-CL')}</Descriptions.Item>
        </Descriptions>
      </Card>

      <Divider />

      <Card
        size="small"
        title={
          <Space>
            <RobotOutlined />
            <span>Chat contextual</span>
          </Space>
        }
      >
        <div style={{ maxHeight: 400, overflowY: 'auto', marginBottom: 16 }}>
          {mensajes.length === 0 && !chatLoading && (
            <Typography.Text type="secondary" style={{ display: 'block', textAlign: 'center', padding: 16 }}>
              Haz una pregunta sobre el análisis
            </Typography.Text>
          )}
          <List
            size="small"
            dataSource={mensajes}
            loading={chatLoading}
            renderItem={(msg) => (
              <List.Item>
                <Space align="start" style={{ width: '100%' }}>
                  {msg.rol === 'user' ? <UserOutlined /> : <RobotOutlined />}
                  <div style={{ flex: 1 }}>
                    <Typography.Text style={{ whiteSpace: 'pre-wrap' }}>{msg.contenido}</Typography.Text>
                    <br />
                    <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                      {new Date(msg.createdAt).toLocaleTimeString('es-CL')}
                    </Typography.Text>
                  </div>
                </Space>
              </List.Item>
            )}
          />
          <div ref={chatEndRef} />
        </div>
        <Space.Compact style={{ width: '100%' }}>
          <Input
            value={chatInput}
            onChange={(e) => setChatInput(e.target.value)}
            onPressEnter={handleEnviarChat}
            placeholder="Pregunta sobre el análisis..."
            disabled={chatMutation.isPending}
          />
          <Button
            type="primary"
            icon={<SendOutlined />}
            onClick={handleEnviarChat}
            loading={chatMutation.isPending}
          />
        </Space.Compact>
      </Card>
    </Space>
  )
}
