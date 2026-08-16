import { useState } from 'react';
import { Space, Table, AutoComplete, DatePicker, Button, Tag, Empty, App as AntApp, Card, Typography, Tooltip, Select, Spin, Alert } from 'antd';
import { TeamOutlined, ExperimentOutlined, BulbOutlined, GlobalOutlined } from '@ant-design/icons';
import { PageHeader } from '../components/PageHeader';
import dayjs, { type Dayjs } from 'dayjs';
import { useBuscarCompetidor, useAnalizarCompetidor, useListarCompetidores, useActividadMercado } from '../hooks/useCompetidores';
import { useAreasNegocio } from '../hooks/useCatalogos';
import { ApiError } from '../lib/apiClient';
import type { OfertaCompetidor, AnalisisCompetidorResponse } from '../types/competidores';

const { Text } = Typography;
const { RangePicker } = DatePicker;

export function CompetidoresPage() {
  const { message } = AntApp.useApp();
  const [nombreBuscado, setNombreBuscado] = useState('');
  const [textoEscrito, setTextoEscrito] = useState('');
  const [rango, setRango] = useState<[Dayjs, Dayjs] | null>(null);
  const [ultimoResultado, setUltimoResultado] = useState<AnalisisCompetidorResponse | null>(null);
  const [areaMercado, setAreaMercado] = useState<number | null>(null);

  const { data: listaCompetidores, isLoading: cargandoLista } = useListarCompetidores();
  const { data, isLoading } = useBuscarCompetidor(nombreBuscado);
  const analizar = useAnalizarCompetidor();
  const { data: areasNegocio } = useAreasNegocio();
  const { data: actividadMercado, isLoading: cargandoActividadMercado, refetch: reintentarActividadMercado } = useActividadMercado(
    nombreBuscado || null,
    areaMercado,
    rango ? rango[0].format('YYYY-MM-DD') : dayjs().subtract(6, 'month').format('YYYY-MM-DD'),
    rango ? rango[1].format('YYYY-MM-DD') : dayjs().format('YYYY-MM-DD'),
  );

  const ofertas = data?.data ?? [];
  const sugerencias = (listaCompetidores?.data ?? [])
    .filter((nombre) => nombre.toLowerCase().includes(textoEscrito.toLowerCase()))
    .map((nombre) => ({ value: nombre }));

  const handleSeleccionar = (nombre: string) => {
    setTextoEscrito(nombre);
    setNombreBuscado(nombre);
    setUltimoResultado(null);
  };

  const handleAnalizar = async (confirmar: boolean) => {
    if (!nombreBuscado || !rango) {
      message.warning('Elegí un competidor y un rango de fechas primero');
      return;
    }

    try {
      const { data: resultado } = await analizar.mutateAsync({
        nombreCompetidor: nombreBuscado,
        fechaDesde: rango[0].format('YYYY-MM-DD'),
        fechaHasta: rango[1].format('YYYY-MM-DD'),
        confirmar,
      });

      setUltimoResultado(resultado);

      if (resultado.requiereConfirmacion) {
        // FR-006: mostramos el volumen y esperamos confirmación explícita -- todavía no se
        // gastó ningún token de Gemini en este punto.
        return;
      }

      if (resultado.cacheado) {
        message.success('Análisis ya existía — mostrando el resultado guardado, sin generar uno nuevo');
      } else {
        message.success('Análisis generado con IA');
      }
    } catch (e) {
      // 029-fix-hallazgos-code-review-competidores-alertas (FR-003/US3): 422 = Gemini bloqueó
      // el contenido -- es un caso reintentable, no una falla del sistema, así que se muestra
      // como warning (con más tiempo en pantalla) en vez del error genérico. El texto del
      // mensaje ya viene armado por el backend (contracts/competidores-analisis-api.md).
      if (e instanceof ApiError && e.status === 422) {
        message.warning(e.message, 8);
      } else {
        message.error(e instanceof Error ? e.message : 'No se pudo generar el análisis');
      }
    }
  };

  const columns = [
    {
      title: 'Licitación',
      dataIndex: 'nombreLicitacion',
      key: 'nombreLicitacion',
      render: (nombre: string, row: OfertaCompetidor) => (
        <div>
          <div style={{ fontWeight: 500 }}>{nombre}</div>
          <Text type="secondary" style={{ fontSize: 12 }}>{row.codigoExterno}</Text>
        </div>
      ),
    },
    { title: 'Organismo', dataIndex: 'organismo', key: 'organismo', render: (v: string | null) => v ?? '—' },
    {
      title: 'Monto ofertado',
      dataIndex: 'montoOferta',
      key: 'montoOferta',
      align: 'right' as const,
      render: (v: number | null) => (v !== null && v !== undefined ? `$${v.toLocaleString('es-CL')}` : '—'),
    },
    {
      title: 'Estado',
      dataIndex: 'estadoOferta',
      key: 'estadoOferta',
      render: (v: string | null) =>
        v?.toLowerCase().includes('acept')
          ? <Tag color="success">{v}</Tag>
          : v?.toLowerCase().includes('rechaz')
            ? <Tag color="error">{v}</Tag>
            : <Tag>{v ?? '—'}</Tag>,
    },
  ];

  return (
    <Space direction="vertical" size={20} style={{ width: '100%' }}>
      <PageHeader
        icon={<TeamOutlined />}
        title="Inteligencia de Competencia"
        subtitle="Busca un competidor y revisa en qué licitaciones ha ofertado — el análisis con IA es opcional y se genera solo cuando lo solicitas, nunca automáticamente."
      />

      <Card size="small">
        <Space wrap>
          <AutoComplete
            allowClear
            style={{ width: 320 }}
            value={textoEscrito}
            options={sugerencias}
            onChange={setTextoEscrito}
            onSelect={handleSeleccionar}
            placeholder="Escribe el nombre de un competidor (ej. SON...)"
            notFoundContent={cargandoLista ? 'Cargando...' : 'Sin competidores recolectados todavía'}
          />
        </Space>
      </Card>

      {nombreBuscado && (
        <Table<OfertaCompetidor>
          columns={columns}
          dataSource={ofertas}
          rowKey="licitacionId"
          loading={isLoading}
          pagination={{ pageSize: 10 }}
          locale={{ emptyText: <Empty description={`Sin ofertas recolectadas todavía para "${nombreBuscado}"`} /> }}
        />
      )}

      {nombreBuscado && ofertas.length > 0 && (
        <Card size="small" title={<><ExperimentOutlined /> Análisis con IA (bajo demanda)</>}>
          <Space direction="vertical" style={{ width: '100%' }} size={12}>
            <Space wrap>
              <RangePicker
                value={rango}
                onChange={(v) => setRango(v as [Dayjs, Dayjs] | null)}
                presets={[
                  { label: 'Últimos 6 meses', value: [dayjs().subtract(6, 'month'), dayjs()] },
                  { label: 'Último año', value: [dayjs().subtract(1, 'year'), dayjs()] },
                ]}
              />
              <Button
                icon={<ExperimentOutlined />}
                loading={analizar.isPending}
                onClick={() => handleAnalizar(false)}
              >
                Consultar
              </Button>
            </Space>

            {ultimoResultado?.requiereConfirmacion && (
              <Card size="small" style={{ background: '#fffbe6', borderColor: '#ffe58f' }}>
                <Space direction="vertical">
                  <Text>
                    No hay un análisis guardado para este competidor y rango — entrarían{' '}
                    <strong>{ultimoResultado.cantidadLicitaciones} licitaciones</strong>.
                  </Text>
                  <Tooltip title="Esto va a llamar a Gemini una vez y guardar el resultado para no repetirlo">
                    <Button type="primary" danger loading={analizar.isPending} onClick={() => handleAnalizar(true)}>
                      Confirmar y analizar con IA
                    </Button>
                  </Tooltip>
                </Space>
              </Card>
            )}

            {ultimoResultado?.contenido && (
              <Card size="small" title={ultimoResultado.cacheado ? 'Resultado guardado (sin gasto de IA)' : 'Resultado nuevo'}>
                <Space direction="vertical" size={10} style={{ width: '100%' }}>
                  <Text>{ultimoResultado.contenido.patrones}</Text>
                  <div>
                    <Text strong>Organismos frecuentes: </Text>
                    {ultimoResultado.contenido.organismosFrecuentes?.map((o) => <Tag key={o}>{o}</Tag>)}
                  </div>
                  <Text><Text strong>Monto promedio ofertado: </Text>{ultimoResultado.contenido.montoPromedioOfertado?.toLocaleString('es-CL') ?? '—'}</Text>
                  <Text><Text strong>Tasa de éxito: </Text>{ultimoResultado.contenido.tasaExito}</Text>
                  <div>
                    <Text strong><BulbOutlined /> Recomendaciones:</Text>
                    <ul>
                      {ultimoResultado.contenido.recomendaciones?.map((r, i) => <li key={i}>{r}</li>)}
                    </ul>
                  </div>
                </Space>
              </Card>
            )}
          </Space>
        </Card>
      )}

      {/* US4 (spec 031): actividad total de mercado -- incluye licitaciones donde TIVIT no participó */}
      {nombreBuscado && (
        <Card size="small" title={<><GlobalOutlined /> Actividad total de mercado</>}>
          <Space direction="vertical" style={{ width: '100%' }} size={12}>
            <Space wrap>
              <Select
                placeholder="Área de negocio (acota el cálculo)"
                allowClear
                style={{ width: 220 }}
                value={areaMercado ?? undefined}
                onChange={(v) => setAreaMercado(v ?? null)}
                options={(areasNegocio ?? []).map((a) => ({ value: a.codigo, label: a.nombre }))}
              />
              <Text type="secondary" style={{ fontSize: 12 }}>Usa el mismo rango de fechas de arriba</Text>
            </Space>

            {actividadMercado?.estado === 'generando' && (
              <Space>
                <Spin size="small" />
                <Text type="secondary">Calculando actividad de mercado, puede tardar varios minutos...</Text>
              </Space>
            )}

            {cargandoActividadMercado && !actividadMercado && <Spin size="small" />}

            {actividadMercado?.estado === 'error' && (
              <Alert
                type="error"
                showIcon
                message="No se pudo calcular la actividad de mercado"
                description="El scraper de fondo falló (o quedó estancado). Podés reintentar; si persiste, revisá los logs del API (Scraper:CompetidorMercadoScriptPath)."
                action={
                  <Button size="small" onClick={() => reintentarActividadMercado()} loading={cargandoActividadMercado}>
                    Reintentar
                  </Button>
                }
              />
            )}

            {actividadMercado?.estado === 'listo' && (
              <Space direction="vertical" style={{ width: '100%' }}>
                <Space wrap size={20}>
                  <Text><Text strong>Licitaciones totales: </Text>{actividadMercado.cantidadLicitaciones}</Text>
                  <Text><Text strong>Monto total adjudicado: </Text>${actividadMercado.montoTotalAdjudicado?.toLocaleString('es-CL') ?? 0}</Text>
                </Space>
                <Table
                  size="small"
                  rowKey="licitacionCodigo"
                  dataSource={actividadMercado.licitaciones?.licitaciones ?? []}
                  pagination={{ pageSize: 10 }}
                  locale={{ emptyText: <Empty description="Sin actividad detectada en esta área/período" /> }}
                  columns={[
                    { title: 'Licitación', dataIndex: 'nombre' },
                    { title: 'Código', dataIndex: 'licitacionCodigo', width: 140 },
                    {
                      title: 'Monto ofertado', dataIndex: 'montoOferta', align: 'right' as const, width: 140,
                      render: (v: number | null) => (v != null ? `$${v.toLocaleString('es-CL')}` : '—'),
                    },
                    {
                      title: 'Relación con TIVIT', dataIndex: 'tivitParticipo', width: 160,
                      render: (v: boolean) => v
                        ? <Tag color="blue">Encuentro directo</Tag>
                        : <Tag color="purple">Brecha de mercado</Tag>,
                    },
                  ]}
                />
              </Space>
            )}
          </Space>
        </Card>
      )}
    </Space>
  );
}
