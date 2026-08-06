import { useState, useCallback, useMemo } from 'react';
import { Alert, Space, Flex, theme } from 'antd';
import { FileTextOutlined, StarOutlined, ClockCircleOutlined } from '@ant-design/icons';
import { LicitacionFilterBar } from '../components/LicitacionFilterBar';
import type { SearchMode } from '../components/LicitacionFilterBar';
import { LicitacionesTable } from '../components/LicitacionesTable';
import { NaturalSearchResults } from '../components/NaturalSearchResults';
import { LicitacionDetailDrawer } from '../components/LicitacionDetailDrawer';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import type { StatusBadgeVariant } from '../components/StatusBadge';
import { useLicitaciones, useBuscarNatural, useLicitacionesSeguidas, useEstadisticasEstado } from '../hooks/useLicitaciones';
import { useLicitacionDetalle } from '../hooks/useLicitacionDetalle';
import type { LicitacionResumen, LicitacionFilter } from '../types/licitacion';

// US1 (spec 019): variante de StatusBadge por nombre de estado -- los codigos reales vienen
// del catalogo (V086), se mapea por nombre porque es lo que ya expone useEstadisticasEstado.
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

export function LicitacionesPage() {
  const [filter, setFilter] = useState<LicitacionFilter>(DEFAULT_FILTER);
  const [selectedCodigo, setSelectedCodigo] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  // 018-buscador-inteligente-nl: la barra de búsqueda semántica convive con los filtros
  // estructurados existentes en vez de reemplazarlos -- comparten el mismo selector de Estado.
  const [searchMode, setSearchMode] = useState<SearchMode>('filtros');
  const [naturalQuery, setNaturalQuery] = useState('');
  // Bug crítico (hallado durante grabación de demo, 2026-07-22): sin esto, cada tecla tipeada
  // disparaba una llamada real a Gemini vía buscar-natural -- costo y carga innecesarios. Ahora
  // la búsqueda solo se envía al confirmar con Enter (o al vaciar el campo).
  const [submittedNaturalQuery, setSubmittedNaturalQuery] = useState('');
  const handleNaturalQueryChange = useCallback((q: string) => {
    setNaturalQuery(q);
    if (q === '') setSubmittedNaturalQuery('');
  }, []);

  // 029-fix-hallazgos-code-review-competidores-alertas (FR-009/QA BUG-002): antes solo se leía
  // data/isLoading -- un 500 real (ej. filtro de fecha mal tipado) se veía idéntico a "sin
  // resultados", la tabla quedaba vacía sin ningún aviso. Ahora se distingue explícitamente.
  const { data, isLoading, isError } = useLicitaciones(filter);
  // US2 (spec 031): desglose por estado, acotado por el mismo filtro de área activo
  const { data: estadisticasEstado } = useEstadisticasEstado(filter.area, filter.sinClasificar);
  const { data: naturalData, isLoading: naturalLoading } = useBuscarNatural(
    submittedNaturalQuery, 1, 20, filter.estado ?? undefined);
  const { data: detalle, isLoading: detalleLoading } = useLicitacionDetalle(selectedCodigo);
  const { data: seguidasData } = useLicitacionesSeguidas();

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

  const seguidasCount = useMemo(() => seguidasData?.length ?? 0, [seguidasData]);

  const closingSoonCount = useMemo(() => {
    return licitaciones.filter(l => {
      if (!l.fechaCierre) return false;
      const diff = new Date(l.fechaCierre).getTime() - Date.now();
      return diff > 0 && diff < 3 * 24 * 60 * 60 * 1000;
    }).length;
  }, [licitaciones]);

  const handleFilterChange = useCallback((partial: Partial<LicitacionFilter>) => {
    setFilter(prev => ({ ...prev, ...partial, page: 1 }));
  }, []);

  const handleResetFilters = useCallback(() => {
    setFilter(DEFAULT_FILTER);
  }, []);

  const handlePageChange = useCallback((page: number, pageSize: number) => {
    setFilter(prev => ({ ...prev, page, pageSize }));
  }, []);

  const handleSortChange = useCallback((sortBy: string, sortDir: 'asc' | 'desc') => {
    setFilter(prev => ({ ...prev, sortBy, sortDir, page: 1 }));
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

  const { token } = theme.useToken();

  return (
    <Space direction="vertical" size={10} style={{ width: '100%' }}>

      {/* ---- Page Header ---- */}
      {/* Metrics como `actions` del header (misma fila que el título) en vez de una fila
          propia debajo -- una fila aparte para solo 3 chips angostos dejaba mucho espacio
          vacío a la derecha en pantallas anchas (ajustado 2026-08-05). */}
      <PageHeader
        icon={<FileTextOutlined />}
        title="Licitaciones"
        subtitle={totalRecords > 0 ? `${totalRecords.toLocaleString('es-CL')} licitaciones` : undefined}
        actions={
          <Flex wrap gap={10}>
            <Flex align="center" gap={8} style={{ background: token.colorFillTertiary, borderRadius: token.borderRadius, padding: '8px 14px' }}>
              <FileTextOutlined style={{ color: token.colorInfo, fontSize: 16 }} />
              <span style={{ fontSize: 13, color: token.colorTextSecondary }}>Disponibles</span>
              <span style={{ fontSize: 16, fontWeight: 700 }}>{totalRecords.toLocaleString('es-CL')}</span>
            </Flex>
            <Flex align="center" gap={8} style={{ background: token.colorFillTertiary, borderRadius: token.borderRadius, padding: '8px 14px' }}>
              <StarOutlined style={{ color: token.colorWarning, fontSize: 16 }} />
              <span style={{ fontSize: 13, color: token.colorTextSecondary }}>Seguidas</span>
              <span style={{ fontSize: 16, fontWeight: 700 }}>{seguidasCount}</span>
            </Flex>
            <Flex align="center" gap={8} style={{ background: token.colorFillTertiary, borderRadius: token.borderRadius, padding: '8px 14px' }}>
              <ClockCircleOutlined style={{ color: token.colorError, fontSize: 16 }} />
              <span style={{ fontSize: 13, color: token.colorTextSecondary }}>Cierran pronto (en esta pág.)</span>
              <span style={{ fontSize: 16, fontWeight: 700 }}>{closingSoonCount}</span>
            </Flex>
          </Flex>
        }
      />

      {/* ---- Filters (búsqueda única + reiniciar) ---- */}
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
        />
      </div>

      {/* ---- Estadísticas por estado, con drill-down (US2 spec 031 / US1 spec 019) --
           Flex wrap en vez de Row/Col de ancho fijo: sin huecos de alineación cualquiera sea
           la cantidad de estados (antes, con md=4 -> 6 columnas, 5 estados dejaban un hueco
           vacío junto al último, ej. "Revocada"). StatusBadge reemplaza las Card+Statistic
           pesadas -- mismo dato, mismo drill-down al hacer clic, menor peso visual. ---- */}
      {estadisticasEstado && estadisticasEstado.length > 0 && (
        <Flex wrap gap={8} data-testid="estadisticas-estado">
          {estadisticasEstado.map(e => (
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
        />
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
