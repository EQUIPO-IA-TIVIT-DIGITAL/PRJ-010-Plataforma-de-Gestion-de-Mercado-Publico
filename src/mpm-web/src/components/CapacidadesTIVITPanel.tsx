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
  FilePdfOutlined,
  ThunderboltOutlined,
  SafetyCertificateOutlined,
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

function getSkillLevelTag(nivel?: number | null, texto?: string | null) {
  if (!nivel && !texto) return null;
  const label = texto || (nivel === 1 ? 'Básico' : nivel === 2 ? 'Intermedio' : nivel === 3 ? 'Avanzado' : nivel === 4 ? 'Experto' : `Nivel ${nivel}`);
  const color = nivel === 4 ? '#cf1322' : nivel === 3 ? '#0958d9' : nivel === 2 ? '#08979c' : '#595959';
  const bg = nivel === 4 ? '#fff1f0' : nivel === 3 ? '#e6f4ff' : nivel === 2 ? '#e6fffb' : '#f5f5f5';
  return (
    <span
      style={{
        fontSize: 10,
        marginLeft: 6,
        padding: '1px 5px',
        borderRadius: 4,
        fontWeight: 600,
        color,
        background: bg,
        border: `1px solid ${color}33`,
      }}
    >
      {label}
    </span>
  );
}

/** Componente diferenciador para Skills y Tecnologías con su nivel de dominio. */
function SkillsTags({ persona }: { persona: CensoPersona }) {
  const skills = persona.skillsDetalle && persona.skillsDetalle.length > 0
    ? persona.skillsDetalle
    : (persona.skills || []).map((s) => ({ nombre: s, nivel: null, nivelTexto: null }));

  if (skills.length === 0) return null;

  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', marginTop: 4 }}>
      <span style={{ fontSize: 11, fontWeight: 600, color: '#0958d9', minWidth: 90, display: 'inline-flex', alignItems: 'center', gap: 4 }}>
        <ThunderboltOutlined style={{ color: '#1677ff' }} /> Skills:
      </span>
      <Space size={[6, 4]} wrap>
        {skills.map((s) => (
          <Tag
            key={s.nombre}
            color="blue"
            style={{
              marginInlineEnd: 0,
              fontSize: 11,
              padding: '2px 8px',
              display: 'inline-flex',
              alignItems: 'center',
            }}
          >
            <span>{s.nombre}</span>
            {getSkillLevelTag(s.nivel, s.nivelTexto)}
          </Tag>
        ))}
      </Space>
    </div>
  );
}

/** Componente diferenciador para Certificaciones Acreditadas con visualización de archivo PDF. */
function CertificacionesTags({ persona }: { persona: CensoPersona }) {
  const { message } = AntdApp.useApp();
  const certs = persona.certificacionesDetalle && persona.certificacionesDetalle.length > 0
    ? persona.certificacionesDetalle
    : (persona.certificaciones || []).map((c) => ({ nombre: c, fileId: null }));

  if (certs.length === 0) return null;

  const handleDescargar = async (fileId: string, _nombre: string) => {
    try {
      const token = localStorage.getItem('mpm_auth_token') || sessionStorage.getItem('mpm_auth_token');
      const res = await fetch(`/api/v1/censo/certificaciones/archivo/${encodeURIComponent(fileId)}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      });
      if (!res.ok) throw new Error('No se pudo obtener el archivo de certificación desde Census');
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Error al abrir certificación');
    }
  };

  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', marginTop: 4 }}>
      <span style={{ fontSize: 11, fontWeight: 600, color: '#531dab', minWidth: 90, display: 'inline-flex', alignItems: 'center', gap: 4 }}>
        <SafetyCertificateOutlined style={{ color: '#722ed1' }} /> Certificaciones:
      </span>
      <Space size={[6, 4]} wrap>
        {certs.map((c) => (
          <Tooltip key={c.nombre} title={c.fileId ? 'Hacer clic para ver documento PDF oficial' : 'Certificación registrada en Census'}>
            <Tag
              color="purple"
              style={{
                marginInlineEnd: 0,
                fontSize: 11,
                padding: '2px 8px',
                cursor: c.fileId ? 'pointer' : 'default',
                userSelect: 'none',
                display: 'inline-flex',
                alignItems: 'center',
                gap: 6,
              }}
              onClick={c.fileId ? () => void handleDescargar(c.fileId!, c.nombre) : undefined}
            >
              <span>{c.nombre}</span>
              {c.fileId && <FilePdfOutlined style={{ color: '#ff4d4f', fontSize: 12 }} />}
            </Tag>
          </Tooltip>
        ))}
      </Space>
    </div>
  );
}

/** Tags genéricos simples para resúmenes */
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
        onSuccess: () => message.success(`Filtro por país ${on ? 'activado' : 'desactivado'}`),
        onError: () => message.error('No se pudo guardar la preferencia'),
      },
    );
  };

  const cambiarPais = (pais: string) => {
    actualizarPrefs.mutate(
      { filtrarPais: pref.filtrarPais, pais },
      {
        onSuccess: () => message.success(`País cambiado a ${pais}`),
        onError: () => message.error('No se pudo guardar la preferencia'),
      },
    );
  };

  const lanzarMatch = (tecnologiasOverride?: string[]) => {
    if (!codigoExterno) return;
    setSinRequisitosInfo(null);
    const body = tecnologiasOverride && tecnologiasOverride.length > 0
      ? { tecnologias: tecnologiasOverride, filtrarPais: pref.filtrarPais, pais: pref.pais }
      : undefined;

    ejecutar.mutate(
      { codigoExterno, body },
      {
        onSuccess: (res) => {
          const total = res.data?.resumen?.totalPersonas ?? 0;
          message.success(`Match completado: ${total} personas encontradas`);
        },
        onError: (err) => {
          const msg = err instanceof Error ? err.message : 'Error al ejecutar match';
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
              Tecnologías y habilidades evaluadas:
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
            <List.Item style={{ padding: '12px 0', alignItems: 'flex-start', borderBottom: '1px solid #f0f0f0' }} data-testid="item-persona-censo">
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
                  <Space direction="vertical" size={4} style={{ width: '100%', marginTop: 2 }}>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {p.cargo} · {p.email}
                    </Typography.Text>
                    <SkillsTags persona={p} />
                    <CertificacionesTags persona={p} />
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
          onClick={() => lanzarMatch()}
          data-testid="btn-ejecutar-match"
        >
          {match ? 'Recalcular match' : 'Buscar capacidades para esta licitación'}
        </Button>
        {match && (
          <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isLoading}>
            Refrescar
          </Button>
        )}
      </Space>

      {sinRequisitosInfo && (
        <Alert
          type="info"
          showIcon
          icon={<InfoCircleOutlined />}
          message="Licitación sin requerimientos de habilidades TI detectados"
          description={
            <div>
              <p style={{ margin: '0 0 8px 0' }}>{sinRequisitosInfo}</p>
              <Typography.Text strong style={{ fontSize: 12 }}>
                Ingresa tecnologías o perfiles a consultar (ej: Linux, Redes, Cloud, Hardware):
              </Typography.Text>
              <Space style={{ width: '100%', marginTop: 6 }} wrap>
                <Select
                  mode="tags"
                  style={{ minWidth: 280 }}
                  placeholder="Escribe tecnologías y presiona Enter"
                  value={tecnologiasManuales}
                  onChange={setTecnologiasManuales}
                />
                <Button
                  type="primary"
                  icon={<PlusOutlined />}
                  loading={ejecutar.isPending}
                  disabled={tecnologiasManuales.length === 0}
                  onClick={() => lanzarMatch(tecnologiasManuales)}
                >
                  Buscar con estas tecnologías
                </Button>
              </Space>
            </div>
          }
          style={{ marginBottom: 16 }}
        />
      )}

      {isLoading ? (
        <div style={{ textAlign: 'center', padding: 40 }}>
          <Spin tip="Cargando capacidades..." />
        </div>
      ) : error ? (
        <Alert
          type="error"
          showIcon
          message="Error al cargar match de capacidades"
          description={error instanceof Error ? error.message : 'Error desconocido'}
          action={<Button size="small" onClick={() => refetch()}>Reintentar</Button>}
        />
      ) : (
        <Card size="small">
          {renderResumen()}
          {renderPersonas()}
        </Card>
      )}
    </div>
  );
}
