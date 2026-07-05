import { useState } from 'react';
import { Space, Typography, Card, Tabs, Table, Drawer, Empty } from 'antd';
import { useCatalogos } from '../hooks/useCatalogos';
import type { EstadoItem, TipoLicitacionItem, MonedaItem } from '../types/catalogo';
import { CheckCircleOutlined, DollarOutlined, AppstoreOutlined, DatabaseOutlined, BulbOutlined } from '@ant-design/icons';
import { descripcionEstado, descripcionTipo, type CatalogoDescripcion } from '../constants/catalogoDescripciones';

const STATUS_CONFIG: Record<number, { color: string; bg: string }> = {
  1: { color: '#3b82f6', bg: '#eff6ff' },
  2: { color: '#f59e0b', bg: '#fffbeb' },
  3: { color: '#64748b', bg: '#f8fafc' },
  4: { color: '#ef4444', bg: '#fef2f2' },
  5: { color: '#10b981', bg: '#f0fdf4' },
  6: { color: '#64748b', bg: '#f8fafc' },
  7: { color: '#8b5cf6', bg: '#faf5ff' },
  8: { color: '#f59e0b', bg: '#fffbeb' },
};

export function CatalogoPage() {
  const { data, isLoading } = useCatalogos();
  const [drawerDesc, setDrawerDesc] = useState<{ nombre: string; desc?: CatalogoDescripcion } | null>(null);

  const abrirEstado = (record: EstadoItem) =>
    setDrawerDesc({ nombre: record.nombre, desc: descripcionEstado(record.codigo) });
  const abrirTipo = (record: TipoLicitacionItem) =>
    setDrawerDesc({ nombre: record.nombre, desc: descripcionTipo(record.slug ?? record.nombre) });

  const estadosColumns = [
    {
      title: 'Código',
      dataIndex: 'codigo',
      key: 'codigo',
      width: 80,
      align: 'center' as const,
      render: (v: number) => (
        <span style={{ fontFamily: 'monospace', fontSize: 12, fontWeight: 700, color: '#3b82f6', background: '#eff6ff', padding: '2px 8px', borderRadius: 6 }}>
          {v}
        </span>
      ),
    },
    {
      title: 'Nombre',
      dataIndex: 'nombre',
      key: 'nombre',
      render: (v: string) => <span style={{ fontWeight: 500, fontSize: 13 }}>{v}</span>,
    },
    {
      title: 'Estado',
      key: 'tag',
      width: 180,
      render: (_: unknown, record: EstadoItem) => {
        const cfg = STATUS_CONFIG[record.codigo] ?? { color: '#64748b', bg: '#f8fafc' };
        return (
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5, padding: '4px 10px', borderRadius: 999, fontSize: 11, fontWeight: 600, color: cfg.color, background: cfg.bg }}>
            <span style={{ width: 6, height: 6, borderRadius: '50%', background: cfg.color, flexShrink: 0 }} />
            {record.nombre}
          </span>
        );
      },
    },
  ];

  const tiposColumns = [
    {
      title: 'Código',
      dataIndex: 'codigo',
      key: 'codigo',
      width: 80,
      align: 'center' as const,
      render: (v: number) => (
        <span style={{ fontFamily: 'monospace', fontSize: 12, fontWeight: 700, color: '#3b82f6', background: '#eff6ff', padding: '2px 8px', borderRadius: 6 }}>
          {v}
        </span>
      ),
    },
    {
      title: 'Nombre',
      dataIndex: 'nombre',
      key: 'nombre',
      render: (v: string) => <span style={{ fontWeight: 500, fontSize: 13 }}>{v}</span>,
    },
    {
      title: 'Slug',
      dataIndex: 'slug',
      key: 'slug',
      width: 180,
      render: (slug: string) => (
        <span style={{ fontFamily: 'monospace', fontSize: 12, color: '#8b5cf6', background: '#faf5ff', padding: '3px 8px', borderRadius: 6 }}>
          {slug}
        </span>
      ),
    },
  ];

  const monedasColumns = [
    {
      title: 'Código',
      dataIndex: 'codigo',
      key: 'codigo',
      width: 80,
      align: 'center' as const,
      render: (v: number) => (
        <span style={{ fontFamily: 'monospace', fontSize: 12, fontWeight: 700, color: '#3b82f6', background: '#eff6ff', padding: '2px 8px', borderRadius: 6 }}>
          {v}
        </span>
      ),
    },
    {
      title: 'Nombre',
      dataIndex: 'nombre',
      key: 'nombre',
      render: (v: string) => <span style={{ fontWeight: 500, fontSize: 13 }}>{v}</span>,
    },
    {
      title: 'Símbolo',
      dataIndex: 'simbolo',
      key: 'simbolo',
      width: 80,
      align: 'center' as const,
      render: (s: string) => (
        <span style={{ fontSize: 16, fontWeight: 700, color: '#0f172a' }}>{s}</span>
      ),
    },
    {
      title: 'ISO',
      dataIndex: 'codigoIso',
      key: 'codigoIso',
      width: 100,
      render: (iso: string) => (
        <span style={{ fontFamily: 'monospace', fontSize: 12, fontWeight: 700, color: '#10b981', background: '#f0fdf4', padding: '3px 8px', borderRadius: 6 }}>
          {iso}
        </span>
      ),
    },
  ];

  const tabItems = [
    {
      key: 'estados',
      label: (
        <span style={{ display: 'flex', alignItems: 'center', gap: 6, fontWeight: 500 }}>
          <CheckCircleOutlined style={{ color: '#10b981' }} />
          Estados de Licitación
        </span>
      ),
      children: (
        <Table<EstadoItem>
          columns={estadosColumns}
          dataSource={data?.estadosLicitacion ?? []}
          rowKey="codigo"
          loading={isLoading}
          size="middle"
          pagination={false}
          data-testid="catalogo-estados-table"
          style={{ borderRadius: 10, overflow: 'hidden' }}
          onRow={(record) => ({ onClick: () => abrirEstado(record), style: { cursor: 'pointer' } })}
        />
      ),
    },
    {
      key: 'tipos',
      label: (
        <span style={{ display: 'flex', alignItems: 'center', gap: 6, fontWeight: 500 }}>
          <AppstoreOutlined style={{ color: '#8b5cf6' }} />
          Tipos de Licitación
        </span>
      ),
      children: (
        <Table<TipoLicitacionItem>
          columns={tiposColumns}
          dataSource={data?.tiposLicitacion ?? []}
          rowKey="codigo"
          loading={isLoading}
          size="middle"
          pagination={false}
          data-testid="catalogo-tipos-table"
          style={{ borderRadius: 10, overflow: 'hidden' }}
          onRow={(record) => ({ onClick: () => abrirTipo(record), style: { cursor: 'pointer' } })}
        />
      ),
    },
    {
      key: 'monedas',
      label: (
        <span style={{ display: 'flex', alignItems: 'center', gap: 6, fontWeight: 500 }}>
          <DollarOutlined style={{ color: '#f59e0b' }} />
          Monedas
        </span>
      ),
      children: (
        <Table<MonedaItem>
          columns={monedasColumns}
          dataSource={data?.monedas ?? []}
          rowKey="codigo"
          loading={isLoading}
          size="middle"
          pagination={false}
          data-testid="catalogo-monedas-table"
          style={{ borderRadius: 10, overflow: 'hidden' }}
        />
      ),
    },
  ];

  return (
    <Space direction="vertical" size={20} style={{ width: '100%' }}>

      {/* ---- Page Header ---- */}
      <div className="mpm-page-header">
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 4 }}>
            <div style={{ width: 32, height: 32, borderRadius: 8, background: 'linear-gradient(135deg, #f59e0b, #fbbf24)', display: 'flex', alignItems: 'center', justifyContent: 'center', boxShadow: '0 4px 10px rgba(245,158,11,0.3)' }}>
              <DatabaseOutlined style={{ color: 'white', fontSize: 15 }} />
            </div>
            <h1 className="mpm-page-title">Catálogos</h1>
          </div>
          <p className="mpm-page-subtitle">
            Referencia de estados, tipos de licitación y monedas del sistema
          </p>
        </div>

        {/* Summary badges */}
        <div style={{ display: 'flex', gap: 8 }}>
          {data?.estadosLicitacion && (
            <span style={{ padding: '6px 14px', borderRadius: 999, fontSize: 12, fontWeight: 600, background: '#f0fdf4', color: '#10b981', border: '1px solid rgba(16,185,129,0.2)' }}>
              {data.estadosLicitacion.length} estados
            </span>
          )}
          {data?.tiposLicitacion && (
            <span style={{ padding: '6px 14px', borderRadius: 999, fontSize: 12, fontWeight: 600, background: '#faf5ff', color: '#8b5cf6', border: '1px solid rgba(139,92,246,0.2)' }}>
              {data.tiposLicitacion.length} tipos
            </span>
          )}
          {data?.monedas && (
            <span style={{ padding: '6px 14px', borderRadius: 999, fontSize: 12, fontWeight: 600, background: '#fffbeb', color: '#f59e0b', border: '1px solid rgba(245,158,11,0.2)' }}>
              {data.monedas.length} monedas
            </span>
          )}
        </div>
      </div>

      {/* ---- Tabs ---- */}
      <Card style={{ padding: 0 }}>
        <Tabs
          defaultActiveKey="estados"
          items={tabItems}
          data-testid="catalogo-tabs"
          style={{ padding: '0 4px' }}
          tabBarStyle={{ marginBottom: 16, borderBottomColor: 'var(--border)' }}
        />
      </Card>

      {/* ---- Drawer explicativo ---- */}
      <Drawer
        open={drawerDesc !== null}
        onClose={() => setDrawerDesc(null)}
        width={420}
        title={
          <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <BulbOutlined style={{ color: '#f59e0b' }} />
            {drawerDesc?.desc?.titulo ?? drawerDesc?.nombre}
          </span>
        }
        data-testid="catalogo-descripcion-drawer"
      >
        {drawerDesc?.desc ? (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <Typography.Paragraph style={{ fontSize: 14, lineHeight: 1.8, margin: 0 }}>
              {drawerDesc.desc.explicacion}
            </Typography.Paragraph>
            {drawerDesc.desc.ejemplo && (
              <div
                style={{
                  background: '#fffbeb',
                  border: '1px solid #fde68a',
                  borderRadius: 10,
                  padding: '12px 16px',
                }}
              >
                <Typography.Text strong style={{ fontSize: 12, color: '#b45309', display: 'block', marginBottom: 4 }}>
                  Ejemplo
                </Typography.Text>
                <Typography.Text style={{ fontSize: 13, lineHeight: 1.6 }}>
                  {drawerDesc.desc.ejemplo}
                </Typography.Text>
              </div>
            )}
          </Space>
        ) : (
          <Empty description="Sin descripción disponible" image={Empty.PRESENTED_IMAGE_SIMPLE} />
        )}
      </Drawer>
    </Space>
  );
}