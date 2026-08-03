import { useState, useCallback, useMemo } from 'react';
import { Alert, Space, Tag, Card, Row, Col, Statistic } from 'antd';
import { FileTextOutlined, StarOutlined, ClockCircleOutlined, SyncOutlined } from '@ant-design/icons';
import { LicitacionFilterBar } from '../components/LicitacionFilterBar';
import type { SearchMode } from '../components/LicitacionFilterBar';
import { LicitacionesTable } from '../components/LicitacionesTable';
import { NaturalSearchResults } from '../components/NaturalSearchResults';
import { LicitacionDetailDrawer } from '../components/LicitacionDetailDrawer';
import { useLicitaciones, useBuscarNatural, useLicitacionesSeguidas } from '../hooks/useLicitaciones';
import { useLicitacionDetalle } from '../hooks/useLicitacionDetalle';
import type { LicitacionResumen, LicitacionFilter } from '../types/licitacion';

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

  return (
    <Space direction="vertical" size={10} style={{ width: '100%' }}>

      {/* ---- Page Header ---- */}
      <div className="mpm-page-header" style={{ marginBottom: 0 }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div
              style={{
                width: 32,
                height: 32,
                borderRadius: 8,
                background: 'linear-gradient(135deg, #E30613, #ff3a46)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                boxShadow: '0 4px 10px rgba(227,6,19,0.3)',
              }}
            >
              <FileTextOutlined style={{ color: 'white', fontSize: 15 }} />
            </div>
            <h1 className="mpm-page-title">Licitaciones</h1>
            {totalRecords > 0 && (
              <Tag style={{ padding: '4px 12px', borderRadius: 999, fontSize: 12, fontWeight: 600, background: '#f0f4ff', border: '1px solid #c7d7fe', color: '#3b4fd8' }}>
                {totalRecords.toLocaleString('es-CL')} licitaciones
              </Tag>
            )}
          </div>
        </div>
      </div>

      {/* ---- Metrics Cards ---- */}
      <Row gutter={[16, 16]} style={{ marginTop: 8, marginBottom: 8 }}>
        <Col xs={24} md={8}>
          <Card bordered={false} style={{ background: '#ffffff', borderRadius: 14, boxShadow: 'var(--shadow-card)' }}>
            <Statistic
              title={<span style={{ fontSize: 12, fontWeight: 600, textTransform: 'uppercase', color: 'var(--text-secondary)' }}>Licitaciones disponibles</span>}
              value={totalRecords}
              prefix={<FileTextOutlined style={{ color: '#3b82f6', marginRight: 8 }} />}
              valueStyle={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)' }}
            />
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card bordered={false} style={{ background: '#ffffff', borderRadius: 14, boxShadow: 'var(--shadow-card)' }}>
            <Statistic
              title={<span style={{ fontSize: 12, fontWeight: 600, textTransform: 'uppercase', color: 'var(--text-secondary)' }}>Licitaciones seguidas</span>}
              value={seguidasCount}
              prefix={<StarOutlined style={{ color: '#f59e0b', marginRight: 8 }} />}
              valueStyle={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)' }}
            />
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card bordered={false} style={{ background: '#ffffff', borderRadius: 14, boxShadow: 'var(--shadow-card)' }}>
            <Statistic
              title={<span style={{ fontSize: 12, fontWeight: 600, textTransform: 'uppercase', color: 'var(--text-secondary)' }}>Cierran pronto (en esta pág)</span>}
              value={closingSoonCount}
              prefix={<ClockCircleOutlined style={{ color: '#ef4444', marginRight: 8 }} />}
              valueStyle={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)' }}
            />
          </Card>
        </Col>
      </Row>

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
