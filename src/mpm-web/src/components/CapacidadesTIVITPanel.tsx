import { useMemo, useState } from 'react';
import {
  Alert,
  App as AntdApp,
  Button,
  Card,
  Empty,
  List,
  Select,
  Space,
  Spin,
  Switch,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import {
  ReloadOutlined,
  SearchOutlined,
  TeamOutlined,
  InfoCircleOutlined,
  PlusOutlined,
} from '@ant-design/icons';
import {
  useActualizarPreferenciasCenso,
  useEjecutarMatch,
  useMatchCapacidades,
  usePreferenciasCenso,
} from '../hooks/useCenso';
import type { CensoMatchResult, CensoPersona } from '../types/licitacion';

// Selector de país de la preferencia (spec censo.md §3: Paises).
const PAISES = ['Chile', 'Brasil', 'Perú', 'Colombia', 'Argentina', 'Ecuador', 'México', 'Otros'];
const DEFAULT_PAIS = 'Chile';
/** Máximo de personas visibles antes de "Ver más". */
const PERSONAS_VISIBLES = 15;

interface Props {
  codigoExterno: string | null;
}

function formatFecha(iso: string | null): string {
  if (!iso) return '-';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return iso;
  return new Intl.DateTimeFormat('es-CL', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(d);
}

function coberturaColor(p: CensoPersona): string {
  const pct = p.totalRequeridos > 0 ? p.cobertura / p.totalRequeridos : 0;
  if (pct >= 0.7) return 'green';
  if (pct >= 0.4) return 'orange';
  return 'red';
}

/** Skills/certificaciones como tags compactos: primeras N + tooltip con el resto. */
function TagsConTooltip({ items, max, color }: { items: string[]; max: number; color?: string }) {
  if (items.length === 0) return null;
  const visibles = items.slice(0, max);
  const restantes = items.length - visibles.length;
  return (
    <Space size={[4, 0]} wrap>
      {visibles.map((it) => (
        <Tag key={it} color={color} style={{ marginInlineEnd: 0, fontSize: 11 }}>
          {it}
        </Tag>
      ))}
      {restantes > 0 && (
        <Tooltip title={items.slice(max).join(', ')}>
          <Tag style={{ marginInlineEnd: 0, fontSize: 11 }}>+{restantes}</Tag>
        </Tooltip>
      )}
    </Space>
  );
}

/**
 * 036-flujo-comercial-ofertas (Fase 2): match de capacidades TIVIT contra Census.
 * Toggle "Filtrar por país" persistido en preferencias + resultado del match por licitación.
 */
export function CapacidadesTIVITPanel({ codigoExterno }: Props) {
  const { message } = AntdApp.useApp();
  const { data: prefsData, isLoading: prefsLoading } = usePreferenciasCenso();
  const actualizarPrefs = useActualizarPreferenciasCenso();
  const { data, isLoading, error, refetch } = useMatchCapacidades(codigoExterno);
  const ejecutar = useEjecutarMatch();
  const [verTodas, setVerTodas] = useState(false);
  const [tecnologiasManuales, setTecnologiasManuales] = useState<string[]>([]);
  const [sinRequisitosInfo, setSinRequisitosInfo] = useState<string | null>(null);

  // Preferencias con defaults (spec: OFF + Chile) mientras no hay fila persistida.
  const pref = prefsData?.data ?? { filtrarPais: false, pais: DEFAULT_PAIS };
  const estado = data?.data;
  const match: CensoMatchResult | null = estado?.match ?? null;

  const personasOrdenadas = useMemo(
    () => (match ? [...match.personas].sort((a, b) => b.cobertura - a.cobertura) : []),
    [match],
  );

  const cambiarFiltrarPais = (on: boolean) => {
    actualizarPrefs.mutate(
      { filtrarPais: on, pais: pref.pais },
      {
        onSuccess: () =>
          message.success(on ? 'Filtro por país activado' : 'Filtro por país desactivado'),
        onError: (e) => message.error(e instanceof Error ? e.message : 'No se pudo guardar la preferencia'),
      },
    );
  };

  const cambiarPais = (pais: string) => {
    actualizarPrefs.mutate(
      { pais },
      {
        onSuccess: () => message.success(`Filtro por país: ${pais}`),
        onError: (e) => message.error(e instanceof Error ? e.message : 'No se pudo guardar la preferencia'),
      },
    );
  };

  const buscar = (overrideTechs?: string[]) => {
    if (!codigoExterno) return;
    setSinRequisitosInfo(null);

    const techs = overrideTechs ?? tecnologiasManuales;
    const body: { filtrarPais?: boolean; pais?: string; tecnologias?: string[] } = {
      filtrarPais: pref.filtrarPais,
      pais: pref.pais,
    };
    if (techs.length > 0) {
      body.tecnologias = techs;
    }

    ejecutar.mutate(
      { codigoExterno, body },
      {
        onSuccess: (r) => {
          const n = r.data.resumen.totalPersonas;
          message.success(`Match completado: ${n} ${n === 1 ? 'persona encontrada' : 'personas encontradas'}`);
        },
        onError: (e) => {
          const msg = e instanceof Error ? e.message : 'No se pudo ejecutar el match';
          if (msg.includes('CEN_001') || msg.toLowerCase().includes('sin requisitos') || msg.toLowerCase().includes('certificaciones')) {
            setSinRequisitosInfo(
              'Esta licitación no contiene certificaciones ni requisitos tecnológicos TI en sus bases. Puedes ingresar habilidades o perfiles manualmente abajo para consultar en Census.',
            );
          } else {
            message.error(msg);
          }
        },
      },
    );
  };

  const renderResumen = () => {
    if (!match) return null;
    const maxTotal = match.personas.reduce((acc, p) => Math.max(acc, p.totalRequeridos), 0) || 1;
    return (
      <>
        <Space wrap style={{ marginBottom: 8 }}>
          <Tag icon={<TeamOutlined />} color="blue">
            Total: {match.resumen.totalPersonas} personas
          </Tag>
          <Tag color="geekblue">
            Cobertura máx: {match.resumen.maxCobertura}/{maxTotal}
          </Tag>
          <Tag color="green">Cobertura ≥70%: {match.resumen.personasConCoberturaAlta}</Tag>
        </Space>
        {match.tecnologiasExpandidas.length > 0 && (
          <div style={{ marginBottom: 8 }}>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Tecnologías consultadas ({match.consultas} consultas, {match.cacheUsadas} de cache):
            </Typography.Text>
            <div style={{ marginTop: 4 }}>
              <TagsConTooltip items={match.tecnologiasExpandidas} max={6} />
            </div>
          </div>
        )}
        <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginBottom: 8 }}>
          Ejecutado el {formatFecha(match.ejecutadoEn)}
        </Typography.Text>
      </>
    );
  };

  const renderPersonas = () => {
    if (!match || personasOrdenadas.length === 0) {
      return <Empty description="No se encontraron personas con capacidades TIVIT para esta licitación" />;
    }
    const visibles = verTodas ? personasOrdenadas : personasOrdenadas.slice(0, PERSONAS_VISIBLES);
    const ocultas = personasOrdenadas.length - visibles.length;
    return (
      <>
        <List
          size="small"
          dataSource={visibles}
          renderItem={(p) => (
            <List.Item style={{ padding: '8px 0', alignItems: 'flex-start' }} data-testid="item-persona-censo">
              <List.Item.Meta
                title={
                  <Space size={6} wrap>
                    <Tag color={coberturaColor(p)} style={{ marginInlineEnd: 0 }}>
                      {p.cobertura}/{p.totalRequeridos}
                    </Tag>
                    <Typography.Text strong>{p.nombre}</Typography.Text>
                    <Tag style={{ marginInlineEnd: 0 }}>{p.pais}</Tag>
                  </Space>
                }
                description={
                  <Space direction="vertical" size={2} style={{ width: '100%' }}>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {p.cargo} · {p.email}
                    </Typography.Text>
                    <TagsConTooltip items={p.skills} max={4} color="geekblue" />
                    <TagsConTooltip items={p.certificaciones} max={3} color="purple" />
                  </Space>
                }
              />
            </List.Item>
          )}
        />
        {ocultas > 0 && (
          <Button type="link" size="small" onClick={() => setVerTodas(true)} data-testid="btn-ver-mas-personas">
            Ver más ({ocultas} personas)
          </Button>
        )}
        {verTodas && personasOrdenadas.length > PERSONAS_VISIBLES && (
          <Button type="link" size="small" onClick={() => setVerTodas(false)}>
            Ver menos
          </Button>
        )}
      </>
    );
  };

  return (
    <div style={{ padding: '8px 0' }}>
      <div style={{ marginBottom: 16 }}>
        <Typography.Title level={5} style={{ margin: 0 }}>
          Match de Capacidades TIVIT (Census)
        </Typography.Title>
        <Typography.Text type="secondary" style={{ fontSize: 13 }}>
          Cruce automático de los requerimientos y perfiles de la licitación con el catálogo de colaboradores y habilidades de TIVIT.
        </Typography.Text>
      </div>

      <Space wrap style={{ marginBottom: 16 }}>
        <Space size={8}>
          <Typography.Text>Filtrar por país</Typography.Text>
          <Switch
            checked={pref.filtrarPais}
            loading={prefsLoading}
            disabled={prefsLoading}
            onChange={cambiarFiltrarPais}
            data-testid="switch-filtrar-pais"
          />
        </Space>
        {pref.filtrarPais && (
          <Select
            value={pref.pais}
            options={PAISES.map((p) => ({ value: p, label: p }))}
            onChange={cambiarPais}
            style={{ width: 140 }}
            data-testid="select-pais-censo"
          />
        )}
        <Button
          type="primary"
          icon={<SearchOutlined />}
          loading={ejecutar.isPending}
          disabled={!codigoExterno || prefsLoading}
          onClick={() => buscar()}
          data-testid="btn-buscar-capacidades"
        >
          {match ? 'Actualizar match' : 'Buscar capacidades para esta licitación'}
        </Button>
      </Space>

      {sinRequisitosInfo && (
        <Alert
          type="warning"
          showIcon
          icon={<InfoCircleOutlined />}
          style={{ marginBottom: 16 }}
          message="Sin requisitos tecnológicos automáticos"
          description={
            <div>
              <p style={{ margin: '0 0 8px 0' }}>{sinRequisitosInfo}</p>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                <Select
                  mode="tags"
                  style={{ minWidth: 320, flex: 1 }}
                  placeholder="Ej: PostgreSQL, Docker, Gestión de Proyectos, Linux..."
                  value={tecnologiasManuales}
                  onChange={setTecnologiasManuales}
                />
                <Button
                  type="primary"
                  icon={<SearchOutlined />}
                  loading={ejecutar.isPending}
                  disabled={tecnologiasManuales.length === 0}
                  onClick={() => buscar(tecnologiasManuales)}
                >
                  Buscar con estas tecnologías
                </Button>
              </div>
            </div>
          }
        />
      )}

      {isLoading && !estado ? (
        <div style={{ textAlign: 'center', padding: 30 }}>
          <Spin tip="Consultando estado de capacidades..." />
        </div>
      ) : error && !estado ? (
        <Alert
          type="error"
          showIcon
          message="No se pudo consultar el match"
          description={error instanceof Error ? error.message : 'Intente nuevamente'}
          action={
            <Button size="small" icon={<ReloadOutlined />} onClick={() => refetch()}>
              Reintentar
            </Button>
          }
        />
      ) : !estado || estado.estado === 'no_ejecutado' ? (
        !sinRequisitosInfo && (
          <Alert
            type="info"
            showIcon
            message="Aún no se ha buscado capacidades TIVIT"
            description="El match cruza los requisitos de la licitación con el catálogo de colaboradores de Census (skills y certificaciones) para evaluar la cobertura de TIVIT."
          />
        )
      ) : estado.estado === 'error' ? (
        <Alert
          type="error"
          showIcon
          message="El match de capacidades falló"
          description="Census no respondió o no hay requisitos extraíbles del análisis. Intente nuevamente."
          action={
            <Button size="small" icon={<ReloadOutlined />} onClick={() => buscar()}>
              Reintentar
            </Button>
          }
        />
      ) : (
        <>
          {renderResumen()}
          {renderPersonas()}
        </>
      )}
    </div>
  );
}
