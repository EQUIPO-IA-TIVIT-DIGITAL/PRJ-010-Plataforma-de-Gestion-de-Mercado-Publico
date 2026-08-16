import { Alert, App as AntdApp, Button, Collapse, List, Spin, Tag, Typography } from 'antd';
import { ReloadOutlined, RobotOutlined } from '@ant-design/icons';
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
  const map: Record<string, { color: string; label: string }> = {
    strong_go: { color: 'success', label: 'GO fuerte' },
    go: { color: 'green', label: 'GO' },
    no_go: { color: 'orange', label: 'NO GO' },
    strong_no_go: { color: 'error', label: 'NO GO fuerte' },
  };
  const m = map[go] ?? { color: 'default', label: go };
  return <Tag color={m.color}>{m.label}</Tag>;
}

function TextoOLista(v: unknown, fallback: string): string {
  const s = str(v);
  if (s) return s;
  const list = strList(v);
  if (list.length) return list.join(', ');
  return fallback;
}

function ItemsKeyValue(obj: Record<string, unknown> | null, orden: string[]): { k: string; v: string }[] {
  if (!obj) return [];
  return orden
    .filter((k) => obj[k] != null && obj[k] !== '')
    .map((k) => ({ k, v: TextoOLista(obj[k], '-') }));
}

interface Props {
  codigoExterno: string | null;
}

export function AnalisisComercialPanel({ codigoExterno }: Props) {
  const { message } = AntdApp.useApp();
  const { data, isLoading } = useAnalisisComercialEstado(codigoExterno);
  const iniciar = useIniciarAnalisisComercial();
  const estado = data?.data;

  const disparar = () => {
    if (!codigoExterno) return;
    iniciar.mutate(codigoExterno, {
      onSuccess: (r) => {
        const res = r.data;
        if (res.cacheHit) message.info('Análisis recuperado de cache (los documentos no cambiaron)');
        else message.success('Análisis iniciado (puede tardar unos minutos)');
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

    const datosClave = [
      ...ItemsKeyValue(ident, ['nombre_licitacion', 'codigo_licitacion', 'organismo_demandante', 'unidad_tecnica', 'tipo_licitacion']),
      ...ItemsKeyValue(montos, ['monto_estimado', 'moneda', 'duracion_meses', 'renovacion']),
      ...ItemsKeyValue(fechas, ['fecha_publicacion', 'fecha_cierre', 'fecha_apertura_tecnica', 'fecha_apertura_economica', 'fecha_estimada_adjudicacion']),
    ];

    return (
      <>
        {estado.desactualizado && (
          <Alert
            type="warning"
            showIcon
            style={{ marginBottom: 12 }}
            message="Los documentos cambiaron desde el último análisis"
            description="Descarga los documentos nuevamente y vuelve a analizar para actualizar el resultado."
          />
        )}
        <Typography.Paragraph style={{ marginBottom: 8 }}>{estado.resumenEjecutivo ?? 'Sin resumen ejecutivo.'}</Typography.Paragraph>
        <div style={{ marginBottom: 8 }}>
          {goNoGoTag(estado.goNoGo)}
          {estado.scoreConfianza != null && (
            <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
              Confianza: {(estado.scoreConfianza * 100).toFixed(0)}%
            </Typography.Text>
          )}
          {estado.modeloUsado && (
            <Typography.Text type="secondary" style={{ marginLeft: 8, fontSize: 12 }}>
              {estado.modeloUsado}
              {estado.tokensEntrada != null ? ` · ${estado.tokensEntrada.toLocaleString('es-CL')} tokens` : ''}
            </Typography.Text>
          )}
        </div>

        <Collapse
          size="small"
          style={{ marginTop: 8 }}
          items={[
            {
              key: 'clave',
              label: 'Datos clave',
              children: (
                <List
                  size="small"
                  dataSource={datosClave}
                  renderItem={(it) => (
                    <List.Item style={{ padding: '4px 0' }}>
                      <Typography.Text strong style={{ width: 160, display: 'inline-block' }}>{it.k}</Typography.Text>
                      <Typography.Text>{it.v}</Typography.Text>
                    </List.Item>
                  )}
                />
              ),
            },
            {
              key: 'requisitos',
              label: 'Requisitos',
              children: (
                <>
                  <Typography.Text strong>Administrativos</Typography.Text>
                  <List
                    size="small"
                    dataSource={[
                      ...strList(reqAdmin?.documentos_obligatorios).map((x) => ({ tipo: 'Documento', v: x })),
                      ...strList(reqAdmin?.garantias).map((x) => ({ tipo: 'Garantía', v: x })),
                      ...strList(reqAdmin?.seguros).map((x) => ({ tipo: 'Seguro', v: x })),
                    ]}
                    renderItem={(it) => (
                      <List.Item style={{ padding: '2px 0' }}>
                        <Tag style={{ marginRight: 8 }}>{it.tipo}</Tag>
                        <Typography.Text>{it.v}</Typography.Text>
                      </List.Item>
                    )}
                  />
                  <Typography.Text strong style={{ display: 'block', marginTop: 8 }}>
                    Técnicos
                  </Typography.Text>
                  <List
                    size="small"
                    dataSource={[
                      ...strList(reqTec?.certificaciones_requeridas).map((x) => ({ tipo: 'Certificación', v: x })),
                      ...strList(reqTec?.personal_requerido).map((x) => ({ tipo: 'Personal', v: x })),
                      ...strList(reqTec?.infraestructura_requerida).map((x) => ({ tipo: 'Infraestructura', v: x })),
                    ]}
                    renderItem={(it) => (
                      <List.Item style={{ padding: '2px 0' }}>
                        <Tag style={{ marginRight: 8 }}>{it.tipo}</Tag>
                        <Typography.Text>{it.v}</Typography.Text>
                      </List.Item>
                    )}
                  />
                  {str(reqTec?.experiencia_minima) && (
                    <Typography.Paragraph style={{ marginTop: 8 }} type="secondary">
                      Experiencia mínima: {str(reqTec?.experiencia_minima)}
                    </Typography.Paragraph>
                  )}
                </>
              ),
            },
            {
              key: 'criterios',
              label: 'Criterios de evaluación',
              children: (
                <List
                  size="small"
                  dataSource={criterios}
                  renderItem={(c) => (
                    <List.Item style={{ padding: '4px 0' }}>
                      <Typography.Text strong>{str(c.nombre) ?? 'Criterio'}</Typography.Text>
                      <Typography.Text style={{ marginLeft: 8 }}>
                        {c.ponderacion_porcentaje != null ? `${c.ponderacion_porcentaje}%` : ''}
                      </Typography.Text>
                    </List.Item>
                  )}
                />
              ),
            },
            {
              key: 'match',
              label: 'Match TIVIT',
              children: (
                <>
                  {str(match?.puede_ofertar) && <Tag color="blue">Puede ofertar: {str(match?.puede_ofertar)}</Tag>}
                  <Typography.Paragraph style={{ marginTop: 8 }} type="secondary">
                    {str(match?.observaciones) ?? 'Sin observaciones.'}
                  </Typography.Paragraph>
                  {strList(match?.brechas_detectadas).length > 0 && (
                    <>
                      <Typography.Text strong>Brechas detectadas</Typography.Text>
                      <List
                        size="small"
                        dataSource={strList(match?.brechas_detectadas)}
                        renderItem={(b) => (
                          <List.Item style={{ padding: '2px 0' }}>
                            <Typography.Text type="warning">• {b}</Typography.Text>
                          </List.Item>
                        )}
                      />
                    </>
                  )}
                </>
              ),
            },
            {
              key: 'riesgos',
              label: 'Riesgos',
              children: (
                <List
                  size="small"
                  dataSource={riesgos}
                  renderItem={(x) => (
                    <List.Item style={{ padding: '4px 0' }}>
                      <Tag color={str(x.severidad) === 'alta' ? 'red' : str(x.severidad) === 'media' ? 'orange' : 'green'}>
                        {str(x.severidad) ?? 'baja'}
                      </Tag>
                      <Typography.Text strong>{str(x.categoria) ?? 'Riesgo'}: </Typography.Text>
                      <Typography.Text>{str(x.descripcion) ?? ''}</Typography.Text>
                    </List.Item>
                  )}
                />
              ),
            },
            {
              key: 'estimacion',
              label: 'Estimación de oferta (orientativa)',
              children: (
                <>
                  {fmtMoneda(estim?.monto_referencial) && (
                    <Typography.Paragraph>
                      Monto referencial: <Typography.Text strong>{fmtMoneda(estim?.monto_referencial)}</Typography.Text>{' '}
                      {str(estim?.moneda) ?? ''}
                    </Typography.Paragraph>
                  )}
                  {str(estim?.nota) && (
                    <Typography.Paragraph type="warning" style={{ fontSize: 12 }}>
                      ⚠ {str(estim?.nota)}
                    </Typography.Paragraph>
                  )}
                  {strList(estim?.supuestos).length > 0 && (
                    <>
                      <Typography.Text strong>Supuestos</Typography.Text>
                      <List
                        size="small"
                        dataSource={strList(estim?.supuestos)}
                        renderItem={(s) => (
                          <List.Item style={{ padding: '2px 0' }}>
                            <Typography.Text type="secondary">• {s}</Typography.Text>
                          </List.Item>
                        )}
                      />
                    </>
                  )}
                </>
              ),
            },
            {
              key: 'json',
              label: 'JSON completo del análisis',
              children: (
                <pre style={{ fontSize: 11, maxHeight: 300, overflow: 'auto', whiteSpace: 'pre-wrap' }}>
                  {JSON.stringify(estado.resultado, null, 2)}
                </pre>
              ),
            },
          ]}
        />
      </>
    );
  };

  return (
    <>
      <Typography.Title level={5} style={{ marginTop: 24 }}>
        Análisis con IA
      </Typography.Title>
      {isLoading && !estado ? (
        <Spin />
      ) : !estado || estado.estado === 'pendiente' ? (
        <Alert
          type="info"
          showIcon
          message="Los pliegos aún no se han analizado"
          description="La IA lee todos los documentos descargados y extrae datos clave, requisitos, riesgos, match TIVIT y una recomendación GO/NO GO (la decisión final es humana)."
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
          description="Puede tardar unos minutos (todos los pliegos se envían en una sola llamada). Esta sección se actualiza sola."
        />
      ) : estado.estado === 'error' ? (
        <Alert
          type="error"
          showIcon
          message="El análisis falló"
          description={estado.error ?? 'Intente nuevamente'}
          action={
            <Button size="small" icon={<ReloadOutlined />} onClick={disparar}>
              Reintentar
            </Button>
          }
        />
      ) : (
        renderResultado(estado)
      )}
    </>
  );
}
