import {
  Alert,
  App as AntdApp,
  Button,
  Card,
  Collapse,
  Descriptions,
  List,
  Space,
  Spin,
  Table,
  Tag,
  Typography,
  theme,
} from 'antd';
import {
  ReloadOutlined,
  RobotOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  ExclamationCircleOutlined,
} from '@ant-design/icons';
import { useAnalisisComercialEstado, useIniciarAnalisisComercial } from '../hooks/useAnalisisComercial';
import type { AnalisisComercialEstado } from '../types/licitacion';

function asObj(v: unknown): Record<string, unknown> | null {
  return v && typeof v === 'object' ? (v as Record<string, unknown>) : null;
}

function str(v: unknown): string | null {
  return typeof v === 'string' && v ? v : null;
}

function strList(v: unknown): string[] {
  if (Array.isArray(v)) return v.filter((x): x is string => typeof x === 'string');
  return [];
}

function fmtMoneda(v: unknown): string | null {
  if (v == null) return null;
  if (typeof v === 'number') return v.toLocaleString('es-CL');
  return String(v);
}

function goNoGoTag(go: string | null) {
  if (!go) return null;
  const map: Record<string, { color: string; label: string; icon: React.ReactNode }> = {
    strong_go: { color: 'success', label: 'GO FUERTE', icon: <CheckCircleOutlined /> },
    go: { color: 'green', label: 'GO', icon: <CheckCircleOutlined /> },
    no_go: { color: 'warning', label: 'NO GO', icon: <ExclamationCircleOutlined /> },
    strong_no_go: { color: 'error', label: 'STRONG NO GO', icon: <CloseCircleOutlined /> },
  };
  const m = map[go] ?? { color: 'default', label: go.toUpperCase(), icon: null };
  return (
    <Tag color={m.color} icon={m.icon} style={{ fontSize: 13, padding: '4px 10px', fontWeight: 600 }}>
      {m.label}
    </Tag>
  );
}

const LABELS: Record<string, string> = {
  nombre_licitacion: 'Nombre de la licitación',
  codigo_licitacion: 'Código Mercado Público',
  organismo_demandante: 'Organismo demandante',
  unidad_tecnica: 'Unidad técnica',
  tipo_licitacion: 'Tipo de licitación',
  monto_estimado: 'Monto estimado',
  moneda: 'Moneda',
  duracion_meses: 'Duración (meses)',
  renovacion: 'Renovable',
  presupuesto_publicado: 'Presupuesto publicado',
  fecha_publicacion: 'Fecha de publicación',
  fecha_cierre: 'Fecha de cierre',
  fecha_apertura_tecnica: 'Apertura técnica',
  fecha_apertura_economica: 'Apertura económica',
  fecha_estimada_adjudicacion: 'Estimación de adjudicación',
};

function fmtValor(key: string, v: unknown): string {
  if (v == null || v === '') return '-';
  if (typeof v === 'boolean') return v ? 'Sí' : 'No';
  if (typeof v === 'number') return v.toLocaleString('es-CL');
  if (typeof v === 'string') {
    if (v.toLowerCase() === 'true') return 'Sí';
    if (v.toLowerCase() === 'false') return 'No';
    // Si es formato fecha ISO (e.g. 2026-07-23T16:00:00)
    if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(v)) {
      try {
        const d = new Date(v);
        if (!isNaN(d.getTime())) {
          const pad = (n: number) => String(n).padStart(2, '0');
          return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())} hrs`;
        }
      } catch {
        // fallback
      }
    }
    // Si es formato fecha YYYY-MM-DD
    if (/^\d{4}-\d{2}-\d{2}$/.test(v)) {
      const [y, m, d] = v.split('-');
      return `${d}/${m}/${y}`;
    }
  }
  return String(v);
}

interface Props {
  codigoExterno: string | null;
}

export function AnalisisComercialPanel({ codigoExterno }: Props) {
  const { message } = AntdApp.useApp();
  const { token } = theme.useToken();
  const { data, isLoading } = useAnalisisComercialEstado(codigoExterno);
  const iniciar = useIniciarAnalisisComercial();
  const estado = data?.data;

  const disparar = () => {
    if (!codigoExterno) return;
    iniciar.mutate(codigoExterno, {
      onSuccess: (r) => {
        const res = r.data;
        if (res.cacheHit) message.info('Análisis recuperado de caché (los documentos no cambiaron)');
        else message.success('Análisis comercial con IA iniciado en segundo plano');
      },
      onError: (e) => message.error(e instanceof Error ? e.message : 'No se pudo iniciar el análisis'),
    });
  };

  const renderResultado = (estado: AnalisisComercialEstado) => {
    const r = asObj(estado.resultado);
    const ident = asObj(r?.identificacion);
    const montos = asObj(r?.montos_y_duracion);
    const fechas = asObj(r?.fechas_clave);
    const criterios = Array.isArray(r?.criterios_evaluacion) ? (r!.criterios_evaluacion as Record<string, unknown>[]) : [];
    const reqAdmin = asObj(r?.requisitos_administrativos);
    const reqTec = asObj(r?.requisitos_tecnicos);
    const riesgos = Array.isArray(r?.riesgos) ? (r!.riesgos as Record<string, unknown>[]) : [];
    const match = asObj(r?.match_tivit);
    const estim = asObj(r?.estimacion);

    const puede = str(match?.puede_ofertar)?.toLowerCase();
    const puedeLabel = puede === 'si' ? 'Sí' : puede === 'no' ? 'No' : puede === 'parcial' ? 'Parcial' : (puede ? puede.toUpperCase() : 'No especificado');
    const puedeColor = puede === 'si' ? 'success' : puede === 'no' ? 'error' : 'warning';

    return (
      <div style={{ padding: '4px 0' }}>
        {estado.desactualizado && (
          <Alert
            type="warning"
            showIcon
            style={{ marginBottom: 16 }}
            message="Los documentos cambiaron desde el último análisis"
            description="Se han detectado nuevos documentos o versiones. Vuelve a analizar para actualizar el análisis comercial."
            action={
              <Button size="small" type="primary" onClick={disparar}>
                Re-analizar
              </Button>
            }
          />
        )}

        {/* Resumen Ejecutivo Card */}
        <Card
          size="small"
          style={{
            marginBottom: 20,
            background: token.colorFillAlter,
            borderColor: token.colorBorderSecondary,
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 12, marginBottom: 12 }}>
            <Space size={10} wrap>
              {goNoGoTag(estado.goNoGo)}
              {estado.scoreConfianza != null && (
                <Tag color="blue" style={{ fontSize: 13, padding: '4px 8px' }}>
                  Confianza: {(estado.scoreConfianza * 100).toFixed(0)}%
                </Tag>
              )}
            </Space>

            <Space size={8}>
              {estado.modeloUsado && (
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  Modelo: <code>{estado.modeloUsado}</code>
                  {estado.tokensEntrada != null ? ` · ${estado.tokensEntrada.toLocaleString('es-CL')} tokens` : ''}
                </Typography.Text>
              )}
              <Button size="small" icon={<ReloadOutlined />} onClick={disparar}>
                Re-analizar
              </Button>
            </Space>
          </div>

          <Typography.Title level={5} style={{ margin: '8px 0 4px 0' }}>
            Resumen Ejecutivo
          </Typography.Title>
          <Typography.Paragraph style={{ margin: 0, fontSize: 13, lineHeight: 1.6 }}>
            {estado.resumenEjecutivo ?? 'Sin resumen ejecutivo disponible.'}
          </Typography.Paragraph>
        </Card>

        {/* Secciones de Análisis */}
        <Collapse
          defaultActiveKey={['match', 'riesgos', 'clave']}
          items={[
            {
              key: 'match',
              label: <Typography.Text strong>Match TIVIT y Capacidad de Oferta</Typography.Text>,
              children: (
                <div style={{ padding: '4px 0' }}>
                  <div style={{ marginBottom: 12 }}>
                    <Tag color={puedeColor} style={{ fontSize: 13, padding: '4px 12px', fontWeight: 600 }}>
                      ¿Puede ofertar TIVIT?: {puedeLabel}
                    </Tag>
                  </div>

                  {str(match?.observaciones) && (
                    <Typography.Paragraph style={{ marginBottom: 16, fontSize: 13, lineHeight: 1.6 }}>
                      {str(match?.observaciones)}
                    </Typography.Paragraph>
                  )}

                  {strList(match?.brechas_detectadas).length > 0 && (
                    <Card size="small" style={{ background: '#fffbe6', borderColor: '#ffe58f', marginBottom: 12 }}>
                      <Typography.Text strong style={{ color: '#d48806', display: 'block', marginBottom: 6 }}>
                        Brechas detectadas
                      </Typography.Text>
                      <List
                        size="small"
                        dataSource={strList(match?.brechas_detectadas)}
                        renderItem={(b) => (
                          <List.Item style={{ padding: '3px 0', border: 'none' }}>
                            <Typography.Text type="warning">• {b}</Typography.Text>
                          </List.Item>
                        )}
                      />
                    </Card>
                  )}

                  {strList(match?.requisitos_clave).length > 0 && (
                    <div style={{ marginTop: 8 }}>
                      <Typography.Text strong style={{ display: 'block', marginBottom: 6 }}>
                        Requisitos clave evaluados
                      </Typography.Text>
                      <List
                        size="small"
                        dataSource={strList(match?.requisitos_clave)}
                        renderItem={(r) => (
                          <List.Item style={{ padding: '2px 0', border: 'none' }}>
                            <Typography.Text>✓ {r}</Typography.Text>
                          </List.Item>
                        )}
                      />
                    </div>
                  )}
                </div>
              ),
            },
            {
              key: 'riesgos',
              label: (
                <Typography.Text strong>
                  Riesgos Detectados {riesgos.length > 0 && <Tag color="red" style={{ marginLeft: 6 }}>{riesgos.length}</Tag>}
                </Typography.Text>
              ),
              children: (
                <Table
                  size="small"
                  pagination={false}
                  dataSource={riesgos.map((item, idx) => ({ ...item, key: idx }))}
                  columns={[
                    {
                      title: 'Severidad',
                      dataIndex: 'severidad',
                      width: 110,
                      render: (sev: string) => {
                        const s = (sev || 'baja').toLowerCase();
                        const color = s === 'alta' ? 'error' : s === 'media' ? 'warning' : 'default';
                        const label = s.charAt(0).toUpperCase() + s.slice(1);
                        return <Tag color={color} style={{ fontWeight: 600 }}>{label}</Tag>;
                      },
                    },
                    {
                      title: 'Categoría',
                      dataIndex: 'categoria',
                      width: 180,
                      render: (cat: string) => <Typography.Text strong>{cat || 'General'}</Typography.Text>,
                    },
                    {
                      title: 'Descripción del Riesgo',
                      dataIndex: 'descripcion',
                      render: (desc: string) => <Typography.Text style={{ fontSize: 13 }}>{desc}</Typography.Text>,
                    },
                  ]}
                />
              ),
            },
            {
              key: 'clave',
              label: <Typography.Text strong>Datos Clave de la Licitación</Typography.Text>,
              children: (
                <Descriptions bordered size="small" column={{ xxl: 2, xl: 2, lg: 2, md: 1, sm: 1, xs: 1 }}>
                  {ident &&
                    Object.entries(ident)
                      .filter(([_, v]) => v != null && v !== '')
                      .map(([k, v]) => (
                        <Descriptions.Item key={k} label={LABELS[k] ?? k}>
                          {fmtValor(k, v)}
                        </Descriptions.Item>
                      ))}
                  {montos &&
                    Object.entries(montos)
                      .filter(([_, v]) => v != null && v !== '')
                      .map(([k, v]) => (
                        <Descriptions.Item key={k} label={LABELS[k] ?? k}>
                          {fmtValor(k, v)}
                        </Descriptions.Item>
                      ))}
                  {fechas &&
                    Object.entries(fechas)
                      .filter(([_, v]) => v != null && v !== '')
                      .map(([k, v]) => (
                        <Descriptions.Item key={k} label={LABELS[k] ?? k}>
                          {fmtValor(k, v)}
                        </Descriptions.Item>
                      ))}
                </Descriptions>
              ),
            },
            {
              key: 'requisitos',
              label: <Typography.Text strong>Requisitos Administrativos y Técnicos</Typography.Text>,
              children: (
                <Space direction="vertical" size={16} style={{ width: '100%' }}>
                  <div>
                    <Typography.Text strong style={{ fontSize: 14 }}>
                      Requisitos Administrativos
                    </Typography.Text>
                    <List
                      size="small"
                      dataSource={[
                        ...strList(reqAdmin?.documentos_obligatorios).map((x) => ({ tipo: 'Documento', v: x })),
                        ...strList(reqAdmin?.garantias).map((x) => ({ tipo: 'Garantía', v: x })),
                        ...strList(reqAdmin?.seguros).map((x) => ({ tipo: 'Seguro', v: x })),
                      ]}
                      renderItem={(it) => (
                        <List.Item style={{ padding: '4px 0' }}>
                          <Tag color="geekblue" style={{ marginRight: 8 }}>{it.tipo}</Tag>
                          <Typography.Text>{it.v}</Typography.Text>
                        </List.Item>
                      )}
                    />
                  </div>

                  <div>
                    <Typography.Text strong style={{ fontSize: 14 }}>
                      Requisitos Técnicos
                    </Typography.Text>
                    <List
                      size="small"
                      dataSource={[
                        ...strList(reqTec?.certificaciones_requeridas).map((x) => ({ tipo: 'Certificación', v: x })),
                        ...strList(reqTec?.personal_requerido).map((x) => ({ tipo: 'Personal', v: x })),
                        ...strList(reqTec?.infraestructura_requerida).map((x) => ({ tipo: 'Infraestructura', v: x })),
                      ]}
                      renderItem={(it) => (
                        <List.Item style={{ padding: '4px 0' }}>
                          <Tag color="cyan" style={{ marginRight: 8 }}>{it.tipo}</Tag>
                          <Typography.Text>{it.v}</Typography.Text>
                        </List.Item>
                      )}
                    />
                    {str(reqTec?.experiencia_minima) && (
                      <Typography.Paragraph style={{ marginTop: 8 }} type="secondary">
                        <strong>Experiencia mínima:</strong> {str(reqTec?.experiencia_minima)}
                      </Typography.Paragraph>
                    )}
                  </div>
                </Space>
              ),
            },
            {
              key: 'criterios',
              label: <Typography.Text strong>Criterios de Evaluación</Typography.Text>,
              children: (
                <Table
                  size="small"
                  pagination={false}
                  dataSource={criterios.map((c, i) => ({ ...c, key: i }))}
                  columns={[
                    {
                      title: 'Criterio',
                      dataIndex: 'nombre',
                      render: (n: string) => <Typography.Text strong>{n || 'Criterio'}</Typography.Text>,
                    },
                    {
                      title: 'Ponderación',
                      dataIndex: 'ponderacion_porcentaje',
                      width: 120,
                      render: (p: number) => (p != null ? <Tag color="blue">{p}%</Tag> : '-'),
                    },
                    {
                      title: 'Descripción / Detalle',
                      dataIndex: 'descripcion',
                      render: (d: string) => d || '-',
                    },
                  ]}
                />
              ),
            },
            {
              key: 'estimacion',
              label: <Typography.Text strong>Estimación de Oferta (Orientativa)</Typography.Text>,
              children: (
                <div style={{ padding: '4px 0' }}>
                  {fmtMoneda(estim?.monto_referencial) && (
                    <Typography.Paragraph style={{ fontSize: 14 }}>
                      Monto referencial: <Typography.Text strong style={{ fontSize: 15 }}>{fmtMoneda(estim?.monto_referencial)}</Typography.Text>{' '}
                      {str(estim?.moneda) ?? ''}
                    </Typography.Paragraph>
                  )}
                  {str(estim?.nota) && (
                    <Alert
                      type="warning"
                      showIcon
                      message="Estimación orientativa"
                      description={str(estim?.nota)}
                      style={{ marginBottom: 12 }}
                    />
                  )}
                  {strList(estim?.supuestos).length > 0 && (
                    <>
                      <Typography.Text strong style={{ display: 'block', marginBottom: 6 }}>
                        Supuestos considerados
                      </Typography.Text>
                      <List
                        size="small"
                        dataSource={strList(estim?.supuestos)}
                        renderItem={(s) => (
                          <List.Item style={{ padding: '2px 0', border: 'none' }}>
                            <Typography.Text type="secondary">• {s}</Typography.Text>
                          </List.Item>
                        )}
                      />
                    </>
                  )}
                </div>
              ),
            },
          ]}
        />
      </div>
    );
  };

  return (
    <div style={{ padding: '8px 0' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <div>
          <Typography.Title level={5} style={{ margin: 0 }}>
            Análisis Comercial y Extracción Inteligente
          </Typography.Title>
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            Extracción automática de cláusulas, requisitos técnicos, riesgos y recomendación preliminar de oferta.
          </Typography.Text>
        </div>
      </div>

      {isLoading && !estado ? (
        <div style={{ textAlign: 'center', padding: 40 }}>
          <Spin tip="Consultando estado del análisis comercial..." />
        </div>
      ) : !estado || estado.estado === 'pendiente' ? (
        <Alert
          type="info"
          showIcon
          message="Los pliegos aún no se han analizado"
          description="La IA leerá todos los documentos descargados (PDF y Word) y extraerá los datos clave, requisitos, riesgos, match TIVIT y una recomendación GO/NO GO."
          action={
            <Button size="small" type="primary" icon={<RobotOutlined />} onClick={disparar}>
              Analizar con IA
            </Button>
          }
        />
      ) : estado.estado === 'analizando' ? (
        <Alert
          type="info"
          showIcon
          icon={<Spin size="small" />}
          message="Analizando documentos con IA..."
          description="Se están procesando todos los pliegos y bases. Esta sección se actualizará automáticamente al finalizar."
        />
      ) : estado.estado === 'error' ? (
        <Alert
          type="error"
          showIcon
          message="El análisis falló"
          description={estado.error ?? 'Ocurrió un problema al procesar los documentos'}
          action={
            <Button size="small" icon={<ReloadOutlined />} onClick={disparar}>
              Reintentar
            </Button>
          }
        />
      ) : (
        renderResultado(estado)
      )}
    </div>
  );
}
