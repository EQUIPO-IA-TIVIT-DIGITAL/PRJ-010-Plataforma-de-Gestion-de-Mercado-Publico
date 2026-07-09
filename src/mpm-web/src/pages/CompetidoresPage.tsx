import { useState } from 'react';
import { Space, Table, Input, DatePicker, Button, Tag, Empty, App as AntApp, Card, Typography, Tooltip } from 'antd';
import { TeamOutlined, ExperimentOutlined, BulbOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useBuscarCompetidor, useAnalizarCompetidor } from '../hooks/useCompetidores';
import type { OfertaCompetidor, AnalisisCompetidorResponse } from '../types/competidores';

const { Text } = Typography;
const { RangePicker } = DatePicker;

export function CompetidoresPage() {
  const { message } = AntApp.useApp();
  const [nombreBuscado, setNombreBuscado] = useState('');
  const [inputNombre, setInputNombre] = useState('');
  const [rango, setRango] = useState<[Dayjs, Dayjs] | null>(null);
  const [ultimoResultado, setUltimoResultado] = useState<AnalisisCompetidorResponse | null>(null);

  const { data, isLoading } = useBuscarCompetidor(nombreBuscado);
  const analizar = useAnalizarCompetidor();

  const ofertas = data?.data ?? [];

  const handleBuscar = () => {
    setNombreBuscado(inputNombre.trim());
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
      message.error(e instanceof Error ? e.message : 'No se pudo generar el análisis');
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
      render: (v: number | null) => (v ? `$${v.toLocaleString('es-CL')}` : '—'),
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
      <div className="mpm-page-header">
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 4 }}>
            <div style={{ width: 32, height: 32, borderRadius: 8, background: 'linear-gradient(135deg, #7c3aed, #a78bfa)', display: 'flex', alignItems: 'center', justifyContent: 'center', boxShadow: '0 4px 10px rgba(124,58,237,0.3)' }}>
              <TeamOutlined style={{ color: 'white', fontSize: 15 }} />
            </div>
            <h1 className="mpm-page-title">Inteligencia de Competencia</h1>
          </div>
          <p className="mpm-page-subtitle">
            Buscá un competidor y mirá en qué licitaciones ha ofertado — el análisis con IA es opcional y siempre lo pedís vos, nunca se dispara solo.
          </p>
        </div>
      </div>

      <Card size="small">
        <Space wrap>
          <Input
            placeholder="Nombre del competidor (ej. Sonda)"
            value={inputNombre}
            onChange={(e) => setInputNombre(e.target.value)}
            onPressEnter={handleBuscar}
            style={{ width: 280 }}
          />
          <Button type="primary" onClick={handleBuscar}>Buscar</Button>
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
    </Space>
  );
}
