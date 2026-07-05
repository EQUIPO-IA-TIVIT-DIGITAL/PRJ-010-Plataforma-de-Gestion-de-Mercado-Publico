import { useState, useCallback, useMemo } from 'react';
import { Space, Tag } from 'antd';
import { FileTextOutlined } from '@ant-design/icons';
import { LicitacionFilterBar } from '../components/LicitacionFilterBar';
import { LicitacionesTable } from '../components/LicitacionesTable';
import { LicitacionDetailDrawer } from '../components/LicitacionDetailDrawer';
import { useLicitaciones } from '../hooks/useLicitaciones';
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

  const { data, isLoading } = useLicitaciones(filter);
  const { data: detalle, isLoading: detalleLoading } = useLicitacionDetalle(selectedCodigo);

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

  const totalRecords = data?.data?.totalRecords ?? 0;

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

      {/* ---- Filters (búsqueda única + reiniciar) ---- */}
      <div className="mpm-filter-bar" style={{ padding: '12px 16px' }}>
        <LicitacionFilterBar filter={filter} onChange={handleFilterChange} onReset={handleResetFilters} />
      </div>

      {/* ---- Results ---- */}
      <LicitacionesTable
        dataSource={licitaciones}
        pagination={pagination}
        loading={isLoading}
        onRowClick={handleRowClick}
        onPageChange={handlePageChange}
        onSortChange={handleSortChange}
      />

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
