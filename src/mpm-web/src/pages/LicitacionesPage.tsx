import { useState, useCallback, useMemo, useEffect, useRef } from 'react';
import {
  Alert,
  Button,
  Card,
  Flex,
  Input,
  Radio,
  Segmented,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
  theme,
  message,
} from 'antd';
import {
  FileTextOutlined,
  StarOutlined,
  StarFilled,
  ClockCircleOutlined,
  SearchOutlined,
  ArrowRightOutlined,
  ThunderboltOutlined,
  MessageOutlined,
  BarChartOutlined,
  EyeOutlined,
  PlusOutlined,
  CheckCircleOutlined,
} from '@ant-design/icons';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { LicitacionFilterBar } from '../components/LicitacionFilterBar';
import type { SearchMode } from '../components/LicitacionFilterBar';
import { LicitacionesTable } from '../components/LicitacionesTable';
import { NaturalSearchResults } from '../components/NaturalSearchResults';
import { LicitacionDetailDrawer } from '../components/LicitacionDetailDrawer';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import type { StatusBadgeVariant } from '../components/StatusBadge';
import {
  useLicitaciones,
  useBuscarNatural,
  useLicitacionesSeguidas,
  useSeguirToggle,
  useEstadisticasEstado,
} from '../hooks/useLicitaciones';
import { useLicitacionesInteresListado, useMarcarInteres } from '../hooks/useLicitacionesInteres';
import { useLicitacionDetalle } from '../hooks/useLicitacionDetalle';
import { usePreferenciasLicitaciones } from '../hooks/usePreferenciasLicitaciones';
import type { LicitacionResumen, LicitacionFilter } from '../types/licitacion';

function varianteEstado(nombreEstado: string): StatusBadgeVariant {
  const n = nombreEstado.toLowerCase();
  if (n.includes('adjudicada')) return 'success';
  if (n.includes('desierta')) return 'warning';
  if (n.includes('revocada')) return 'error';
  if (n.includes('publicada')) return 'info';
  return 'neutral';
}

const DEFAULT_FILTER: LicitacionFilter = {
  page: 1,
  pageSize: 20,
  sortBy: 'fecha_publicacion',
  sortDir: 'desc',
};

function formatDate(d: string | null): string {
  if (!d) return '—';
  const parsed = new Date(d);
  if (isNaN(parsed.getTime())) return d;
  return new Intl.DateTimeFormat('es-CL', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(parsed);
}

export function LicitacionesPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const vistaActiva = (searchParams.get('vista') as 'todas' | 'seguidas' | 'interes') || 'todas';

  const [filter, setFilter] = useState<LicitacionFilter>(DEFAULT_FILTER);
  const [selectedCodigo, setSelectedCodigo] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [searchMode, setSearchMode] = useState<SearchMode>('filtros');
  const [naturalQuery, setNaturalQuery] = useState('');
  const [submittedNaturalQuery, setSubmittedNaturalQuery] = useState('');

  // Filtro de búsqueda rápida en pestañas
  const [filtroSeguidas, setFiltroSeguidas] = useState('');
  const [filtroInteres, setFiltroInteres] = useState('');

  // F1-T5: preferencia monto mínimo (D2 siembra frontend, PREF-R001)
  // Si URL no trae montoDesde, sembrar desde preferencia. Override explícito en URL gana.
  // Limpiar filtro = ver todo en sesión, pero recarga reaplica.
  const { data: preferenciasResp, isFetched: preferenciasFetched } = usePreferenciasLicitaciones();
  // Envelope backend: ApiResponse<PreferenciasLicitaciones> -> tipado estricto sin unknown (F-001)
  const preferenciaMontoMinimo = preferenciasResp?.data?.montoMinimo ?? null;
  const preferenciaActiva = preferenciaMontoMinimo;
  const preferenciaAplicadaRef = useRef(false);
  useEffect(() => {
    const urlMonto = searchParams.get('montoDesde');
    const hasUrlOverride = urlMonto !== null && urlMonto !== '';
    // URL explícito siempre gana (PREF-R001), incluso si ya sembramos preferencia
    if (hasUrlOverride) {
      const parsed = Number(urlMonto);
      if (!Number.isNaN(parsed) && filter.montoDesde !== parsed) {
        setFilter((prev) => ({ ...prev, montoDesde: parsed }));
      }
      preferenciaAplicadaRef.current = true;
      return;
    }
    // Sin override en URL: siembra una sola vez desde preferencia
    if (preferenciaAplicadaRef.current) return;
    if (!preferenciasFetched) return;
    if (preferenciaMontoMinimo != null && filter.montoDesde == null) {
      setFilter((prev) => ({ ...prev, montoDesde: preferenciaMontoMinimo }));
    }
    preferenciaAplicadaRef.current = true;
  }, [preferenciaMontoMinimo, preferenciasFetched, searchParams, filter.montoDesde]);

  const { data, isLoading, isError } = useLicitaciones(filter);
  const { data: estadisticasEstado } = useEstadisticasEstado(filter.area, filter.sinClasificar);
  const { data: naturalData, isLoading: naturalLoading } = useBuscarNatural(
    submittedNaturalQuery, 1, 20, filter.estado ?? undefined,
  );
  const { data: detalle, isLoading: detalleLoading } = useLicitacionDetalle(selectedCodigo);
  const { data: seguidasData, isLoading: seguidasLoading } = useLicitacionesSeguidas();
  const { data: interesData, isLoading: interesLoading } = useLicitacionesInteresListado();
  const toggleSeguir = useSeguirToggle();
  const marcarInteres = useMarcarInteres();

  const licitaciones = useMemo(() => data?.data?.items ?? [], [data]);
  const pagination = useMemo(() => {
    const d = data?.data;
    if (!d) return null;
    return {
      page: d.page,
      pageSize: d.pageSize,
      totalRecords: d.totalRecords,
      totalPages: d.totalPages,
      hasNext: d.page < d.totalPages,
      hasPrevious: d.page > 1,
    };
  }, [data]);

  const naturalResults = useMemo(() => naturalData?.data?.items ?? [], [naturalData]);
  const totalRecords = searchMode === 'inteligente'
    ? (naturalData?.data?.totalRecords ?? 0)
    : (data?.data?.totalRecords ?? 0);

  const seguidasLista = useMemo(() => seguidasData ?? [], [seguidasData]);
  const seguidasSet = useMemo(() => new Set(seguidasLista.map((s) => s.codigoExterno)), [seguidasLista]);
  const seguidasFiltradas = useMemo(() => {
    if (!filtroSeguidas.trim()) return seguidasLista;
    const q = filtroSeguidas.toLowerCase();
    return seguidasLista.filter(
      (s) => s.codigoExterno.toLowerCase().includes(q) || s.nombre.toLowerCase().includes(q),
    );
  }, [seguidasLista, filtroSeguidas]);

  const interesLista = useMemo(() => interesData ?? [], [interesData]);
  const interesFiltradas = useMemo(() => {
    if (!filtroInteres.trim()) return interesLista;
    const q = filtroInteres.toLowerCase();
    return interesLista.filter(
      (i) =>
        i.licitacionNombre.toLowerCase().includes(q) ||
        (i.codigoExterno && i.codigoExterno.toLowerCase().includes(q)) ||
        String(i.licitacionId).includes(q) ||
        i.marcadoPor.toLowerCase().includes(q),
    );
  }, [interesLista, filtroInteres]);

  const closingSoonCount = useMemo(() => {
    return licitaciones.filter((l) => {
      if (!l.fechaCierre) return false;
      const diff = new Date(l.fechaCierre).getTime() - Date.now();
      return diff > 0 && diff < 3 * 24 * 60 * 60 * 1000;
    }).length;
  }, [licitaciones]);

  const handleNaturalQueryChange = useCallback((q: string) => {
    setNaturalQuery(q);
    if (q === '') setSubmittedNaturalQuery('');
  }, []);

  const handleFilterChange = useCallback((partial: Partial<LicitacionFilter>) => {
    setFilter((prev) => ({ ...prev, ...partial, page: 1 }));
  }, []);

  const handleResetFilters = useCallback(() => {
    setFilter(DEFAULT_FILTER);
  }, []);

  const handlePageChange = useCallback((page: number, pageSize: number) => {
    setFilter((prev) => ({ ...prev, page, pageSize }));
  }, []);

  const handleSortChange = useCallback((sortBy: string, sortDir: 'asc' | 'desc') => {
    // Default sortDir to 'desc' for monto_estimado (mayores primero)
    const finalSortDir = sortBy === 'monto_estimado' && !sortDir ? 'desc' : sortDir;
    setFilter((prev) => ({ ...prev, sortBy, sortDir: finalSortDir, page: 1 }));
  }, []);

  const handleRowClick = useCallback((row: LicitacionResumen) => {
    setSelectedCodigo(row.codigoExterno);
    setDrawerOpen(true);
  }, []);

  const handleNaturalSelect = useCallback((codigoExterno: string) => {
    setSelectedCodigo(codigoExterno);
    setDrawerOpen(true);
  }, []);

  const handleCloseDrawer = useCallback(() => {
    setDrawerOpen(false);
    setSelectedCodigo(null);
  }, []);

  const cambiarVista = (nuevaVista: 'todas' | 'seguidas' | 'interes') => {
    setSearchParams(nuevaVista === 'todas' ? {} : { vista: nuevaVista });
  };

  const { token } = theme.useToken();

  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      {/* ---- Page Header con KPI Cards Interactivas ---- */}
      <PageHeader
        icon={<FileTextOutlined />}
        title="Licitaciones Mercado Público"
        subtitle="Monitoreo, análisis automático de bases, seguimiento de aclaraciones y gestión de ofertas comerciales."
        actions={
          <Flex wrap gap={10}>
            {/* Card Todas */}
            <div
              onClick={() => cambiarVista('todas')}
              style={{
                background: vistaActiva === 'todas' ? '#eff6ff' : token.colorFillTertiary,
                border: vistaActiva === 'todas' ? '1px solid #3b82f6' : '1px solid transparent',
                borderRadius: token.borderRadius,
                padding: '8px 14px',
                cursor: 'pointer',
                transition: 'all 0.15s',
              }}
            >
              <Flex align="center" gap={8}>
                <FileTextOutlined style={{ color: '#3b82f6', fontSize: 16 }} />
                <span style={{ fontSize: 13, color: token.colorTextSecondary }}>Disponibles</span>
                <span style={{ fontSize: 16, fontWeight: 700 }}>{totalRecords.toLocaleString('es-CL')}</span>
              </Flex>
            </div>

            {/* Card Seguidas (Clickeable) */}
            <div
              onClick={() => cambiarVista('seguidas')}
              style={{
                background: vistaActiva === 'seguidas' ? '#fef3c7' : token.colorFillTertiary,
                border: vistaActiva === 'seguidas' ? '1px solid #f59e0b' : '1px solid transparent',
                borderRadius: token.borderRadius,
                padding: '8px 14px',
                cursor: 'pointer',
                transition: 'all 0.15s',
              }}
            >
              <Flex align="center" gap={8}>
                <StarFilled style={{ color: '#f59e0b', fontSize: 16 }} />
                <span style={{ fontSize: 13, color: token.colorTextSecondary }}>Mis Seguidas</span>
                <span style={{ fontSize: 16, fontWeight: 700, color: '#b45309' }}>{seguidasLista.length}</span>
              </Flex>
            </div>

            {/* Card De Interés (Clickeable) */}
            <div
              onClick={() => cambiarVista('interes')}
              style={{
                background: vistaActiva === 'interes' ? '#f3e8ff' : token.colorFillTertiary,
                border: vistaActiva === 'interes' ? '1px solid #9333ea' : '1px solid transparent',
                borderRadius: token.borderRadius,
                padding: '8px 14px',
                cursor: 'pointer',
                transition: 'all 0.15s',
              }}
            >
              <Flex align="center" gap={8}>
                <ThunderboltOutlined style={{ color: '#9333ea', fontSize: 16 }} />
                <span style={{ fontSize: 13, color: token.colorTextSecondary }}>De Interés</span>
                <span style={{ fontSize: 16, fontWeight: 700, color: '#7e22ce' }}>{interesLista.length}</span>
              </Flex>
            </div>

            {/* Card Cierran pronto */}
            <Flex align="center" gap={8} style={{ background: token.colorFillTertiary, borderRadius: token.borderRadius, padding: '8px 14px' }}>
              <ClockCircleOutlined style={{ color: token.colorError, fontSize: 16 }} />
              <span style={{ fontSize: 13, color: token.colorTextSecondary }}>Cierran pronto</span>
              <span style={{ fontSize: 16, fontWeight: 700 }}>{closingSoonCount}</span>
            </Flex>
          </Flex>
        }
      />

      {/* ---- Selector de Vistas / Pestañas Principales ---- */}
      <Card size="small" style={{ borderRadius: 10, boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
        <Flex justify="space-between" align="center" wrap="wrap" gap={12}>
          <Segmented
            size="large"
            value={vistaActiva}
            onChange={(v) => cambiarVista(v as 'todas' | 'seguidas' | 'interes')}
            options={[
              {
                value: 'todas',
                label: (
                  <span style={{ fontWeight: 600, padding: '0 8px' }}>
                    <FileTextOutlined style={{ marginRight: 6 }} /> Todas las Licitaciones
                  </span>
                ),
              },
              {
                value: 'seguidas',
                label: (
                  <span style={{ fontWeight: 600, padding: '0 8px' }}>
                    <StarFilled style={{ color: '#f59e0b', marginRight: 6 }} /> Mis Seguidas ({seguidasLista.length})
                  </span>
                ),
              },
              {
                value: 'interes',
                label: (
                  <span style={{ fontWeight: 600, padding: '0 8px' }}>
                    <ThunderboltOutlined style={{ color: '#9333ea', marginRight: 6 }} /> Oportunidades de Interés ({interesLista.length})
                  </span>
                ),
              },
            ]}
          />

          {vistaActiva === 'seguidas' && (
            <Input
              placeholder="Buscar en mis seguidas por código o nombre..."
              prefix={<SearchOutlined style={{ color: '#bfbfbf' }} />}
              value={filtroSeguidas}
              onChange={(e) => setFiltroSeguidas(e.target.value)}
              style={{ maxWidth: 360 }}
              allowClear
            />
          )}

          {vistaActiva === 'interes' && (
            <Input
              placeholder="Buscar en mis oportunidades de interés..."
              prefix={<SearchOutlined style={{ color: '#bfbfbf' }} />}
              value={filtroInteres}
              onChange={(e) => setFiltroInteres(e.target.value)}
              style={{ maxWidth: 360 }}
              allowClear
            />
          )}
        </Flex>
      </Card>

      {/* =================================================================== */}
      {/* VISTA 1: TODAS LAS LICITACIONES */}
      {/* =================================================================== */}
      {vistaActiva === 'todas' && (
        <>
          {/* ---- Filters ---- */}
          <div className="mpm-filter-bar" style={{ padding: '12px 16px' }}>
            <LicitacionFilterBar
              filter={filter}
              onChange={handleFilterChange}
              onReset={handleResetFilters}
              searchMode={searchMode}
              onSearchModeChange={setSearchMode}
              naturalQuery={naturalQuery}
              onNaturalQueryChange={handleNaturalQueryChange}
              onNaturalQuerySubmit={() => setSubmittedNaturalQuery(naturalQuery)}
              preferenciaActiva={preferenciaActiva}
            />
          </div>

          {/* ---- Estadísticas por estado ---- */}
          {estadisticasEstado && estadisticasEstado.length > 0 && (
            <Flex wrap gap={8} data-testid="estadisticas-estado">
              {estadisticasEstado.map((e) => (
                <div
                  key={e.codigoEstado}
                  onClick={() => handleFilterChange({ estado: e.codigoEstado })}
                  style={{
                    cursor: 'pointer',
                    outline: filter.estado === e.codigoEstado ? `2px solid ${token.colorPrimary}` : 'none',
                    borderRadius: 999,
                  }}
                  data-testid={`estadistica-estado-${e.codigoEstado}`}
                >
                  <StatusBadge
                    variant={varianteEstado(e.nombreEstado)}
                    label={`${e.nombreEstado}: ${e.cantidad.toLocaleString('es-CL')}`}
                  />
                </div>
              ))}
            </Flex>
          )}

          {/* ---- Results ---- */}
          {searchMode === 'inteligente' ? (
            <NaturalSearchResults
              results={naturalResults}
              loading={naturalLoading}
              query={submittedNaturalQuery}
              onSelect={handleNaturalSelect}
            />
          ) : isError ? (
            <Alert
              type="error"
              showIcon
              message="No se pudieron cargar las licitaciones"
              description="Ocurrió un error al aplicar los filtros. Intenta de nuevo o ajusta los filtros aplicados."
            />
          ) : (
            <LicitacionesTable
              dataSource={licitaciones}
              pagination={pagination}
              loading={isLoading}
              onRowClick={handleRowClick}
              onPageChange={handlePageChange}
              onSortChange={handleSortChange}
              seguidasSet={seguidasSet}
            />
          )}
        </>
      )}

      {/* =================================================================== */}
      {/* VISTA 2: MIS LICITACIONES SEGUIDAS (ESTRELLITAS) */}
      {/* =================================================================== */}
      {vistaActiva === 'seguidas' && (
        <Card size="small" style={{ borderRadius: 10 }}>
          <Alert
            type="warning"
            showIcon
            icon={<StarFilled style={{ color: '#f59e0b' }} />}
            message="Monitoreo Activo de Licitaciones Seguidas"
            description="El sistema vigila automáticamente cada 30 minutos estas licitaciones en Mercado Público para alertarte de nuevas aclaraciones, respuestas a consultas de proveedores o prórrogas en las fechas de cierre."
            style={{ marginBottom: 16 }}
          />

          <Table
            size="small"
            rowKey="codigoExterno"
            loading={seguidasLoading}
            dataSource={seguidasFiltradas}
            pagination={{ pageSize: 15, showSizeChanger: true }}
            columns={[
              {
                title: '⭐',
                key: 'star',
                width: 45,
                align: 'center',
                render: (_, row) => (
                  <Tooltip title="Dejar de seguir">
                    <Button
                      type="text"
                      size="small"
                      icon={<StarFilled style={{ color: '#f59e0b', fontSize: 17 }} />}
                      onClick={() => toggleSeguir.mutate(row.codigoExterno)}
                    />
                  </Tooltip>
                ),
              },
              {
                title: 'Código',
                dataIndex: 'codigoExterno',
                key: 'codigoExterno',
                width: 155,
                render: (codigo: string) => (
                  <span
                    style={{
                      fontFamily: 'monospace',
                      fontSize: 12,
                      fontWeight: 700,
                      color: '#2563eb',
                      background: '#eff6ff',
                      padding: '3px 8px',
                      borderRadius: 6,
                    }}
                  >
                    {codigo}
                  </span>
                ),
              },
              {
                title: 'Nombre de la Licitación',
                dataIndex: 'nombre',
                key: 'nombre',
                render: (nombre: string, row) => (
                  <div>
                    <Typography.Text
                      strong
                      style={{ color: '#0f172a', fontSize: 13, cursor: 'pointer', display: 'block' }}
                      onClick={() => {
                        setSelectedCodigo(row.codigoExterno);
                        setDrawerOpen(true);
                      }}
                    >
                      {nombre}
                    </Typography.Text>
                    <span style={{ fontSize: 11, color: '#64748b' }}>
                      Seguida desde: {formatDate(row.seguidaDesde)}
                    </span>
                  </div>
                ),
              },
              {
                title: 'Publicación',
                dataIndex: 'fechaPublicacion',
                key: 'fechaPublicacion',
                width: 120,
                render: (f: string | null) => <span style={{ fontSize: 12 }}>{formatDate(f)}</span>,
              },
              {
                title: 'Cierre',
                dataIndex: 'fechaCierre',
                key: 'fechaCierre',
                width: 120,
                render: (f: string | null) => (
                  <span style={{ fontSize: 12, fontWeight: 600, color: '#dc2626' }}>
                    {formatDate(f)}
                  </span>
                ),
              },
              {
                title: 'Acciones Rápidas',
                key: 'acciones',
                width: 320,
                render: (_, row) => (
                  <Space size={6} wrap>
                    <Button
                      size="small"
                      icon={<EyeOutlined />}
                      onClick={() => {
                        setSelectedCodigo(row.codigoExterno);
                        setDrawerOpen(true);
                      }}
                    >
                      Ver Ficha
                    </Button>
                    <Button
                      type="primary"
                      size="small"
                      icon={<ThunderboltOutlined />}
                      style={{ background: '#1677ff', color: '#fff', fontWeight: 600 }}
                      onClick={() => navigate(`/licitaciones/${encodeURIComponent(row.codigoExterno)}/oferta`)}
                    >
                      Ficha Comercial
                    </Button>
                    <Button
                      size="small"
                      icon={<BarChartOutlined />}
                      onClick={() => {
                        setSelectedCodigo(row.codigoExterno);
                        setDrawerOpen(true);
                      }}
                    >
                      Pliegos
                    </Button>
                  </Space>
                ),
              },
            ]}
          />
        </Card>
      )}

      {/* =================================================================== */}
      {/* VISTA 3: LICITACIONES DE INTERÉS COMERCIAL */}
      {/* =================================================================== */}
      {vistaActiva === 'interes' && (
        <Card size="small" style={{ borderRadius: 10 }}>
          <Alert
            type="info"
            showIcon
            icon={<ThunderboltOutlined style={{ color: '#9333ea' }} />}
            message="Oportunidades de Negocio Marcadas por el Equipo Comercial"
            description="Estas licitaciones cuentan con análisis de pliegos, salas de discusión y espacios de trabajo para definir la estrategia de GO / NO GO y la propuesta técnica DOCX."
            style={{ marginBottom: 16 }}
          />

          <Table
            size="small"
            rowKey="id"
            loading={interesLoading}
            dataSource={interesFiltradas}
            pagination={{ pageSize: 15, showSizeChanger: true }}
            columns={[
              {
                title: 'Código',
                key: 'codigo',
                width: 150,
                render: (_, row) => (
                  row.codigoExterno ? (
                    <span
                      style={{
                        fontFamily: 'monospace',
                        fontSize: 12,
                        fontWeight: 700,
                        color: '#2563eb',
                        background: '#eff6ff',
                        padding: '3px 8px',
                        borderRadius: 6,
                      }}
                    >
                      {row.codigoExterno}
                    </span>
                  ) : (
                    <Tag color="purple">#{row.licitacionId}</Tag>
                  )
                ),
              },
              {
                title: 'Licitación / Oportunidad',
                dataIndex: 'licitacionNombre',
                key: 'licitacionNombre',
                render: (nombre: string, row) => (
                  <div>
                    <Typography.Text strong style={{ color: '#0f172a', fontSize: 13, display: 'block' }}>
                      {nombre}
                    </Typography.Text>
                    <span style={{ fontSize: 11, color: '#64748b' }}>
                      Marcada por: <b>{row.marcadoPor || 'Comercial'}</b> el {formatDate(row.createdAt)}
                    </span>
                  </div>
                ),
              },
              {
                title: 'Workspace IA',
                key: 'workspace',
                width: 150,
                render: (_, row) => (
                  row.workspaceId ? (
                    <Tag color="green" icon={<BarChartOutlined />}>Workspace #{row.workspaceId}</Tag>
                  ) : (
                    <Tag color="orange">Pendiente</Tag>
                  )
                ),
              },
              {
                title: 'Discusión Grupal',
                key: 'conversacion',
                width: 150,
                render: (_, row) => (
                  row.conversacionId ? (
                    <Tag color="blue" icon={<MessageOutlined />}>Sala #{row.conversacionId}</Tag>
                  ) : (
                    <Tag color="default">Sin sala</Tag>
                  )
                ),
              },
              {
                title: 'Acceso Rápido',
                key: 'acciones',
                width: 360,
                render: (_, row) => {
                  const tieneWorkspace = !!row.workspaceId;
                  const tieneConversacion = !!row.conversacionId;

                  return (
                    <Space size={6} wrap>
                      {/* Si no tiene workspace o chat, botón para generarlos de inmediato */}
                      {(!tieneWorkspace || !tieneConversacion) && (
                        <Button
                          size="small"
                          type="primary"
                          icon={<ThunderboltOutlined />}
                          loading={marcarInteres.isPending}
                          style={{ background: '#7e22ce', color: '#fff' }}
                          onClick={() => {
                            marcarInteres.mutate(
                              { licitacionId: row.licitacionId, nombreLicitacion: row.licitacionNombre },
                              {
                                onSuccess: () => message.success('Workspace y sala de discusión creados exitosamente'),
                                onError: () => message.error('Error al generar workspace y sala de discusión'),
                              },
                            );
                          }}
                        >
                          Generar Workspace & Chat
                        </Button>
                      )}

                      {/* Ir a Ficha Comercial */}
                      {row.codigoExterno && (
                        <Button
                          type="primary"
                          size="small"
                          icon={<ThunderboltOutlined />}
                          style={{ background: '#1677ff', color: '#fff', fontWeight: 600 }}
                          onClick={() => navigate(`/licitaciones/${encodeURIComponent(row.codigoExterno!)}/oferta`)}
                        >
                          Ficha Comercial
                        </Button>
                      )}

                      {/* Ir a Sala de Chat */}
                      {tieneConversacion && (
                        <Button
                          size="small"
                          icon={<MessageOutlined />}
                          onClick={() => navigate(`/mensajes?conversacionId=${row.conversacionId}`)}
                        >
                          Discusión
                        </Button>
                      )}

                      {/* Ir a Workspace IA */}
                      {tieneWorkspace && (
                        <Button
                          size="small"
                          icon={<BarChartOutlined />}
                          onClick={() => navigate(`/analisis/${row.workspaceId}`)}
                        >
                          Workspace
                        </Button>
                      )}
                    </Space>
                  );
                },
              },
            ]}
          />
        </Card>
      )}

      {/* ---- Detail Drawer ---- */}
      <LicitacionDetailDrawer
        open={drawerOpen}
        data={detalle?.data ?? null}
        loading={detalleLoading}
        onClose={handleCloseDrawer}
      />
    </Space>
  );
}
