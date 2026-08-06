import { useMemo } from 'react';
import { Card, Tabs, Table, Space } from 'antd';
import { useCatalogos } from '../hooks/useCatalogos';
import type { EstadoItem, TipoLicitacionItem, MonedaItem } from '../types/catalogo';
import { CheckCircleOutlined, DollarOutlined, AppstoreOutlined, DatabaseOutlined } from '@ant-design/icons';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import type { StatusBadgeVariant } from '../components/StatusBadge';

// US3 (spec 019): mismo mapeo que LicitacionesTable.ESTADO_VARIANT -- via StatusBadge.
const ESTADO_VARIANT: Record<number, StatusBadgeVariant> = {
  1: 'info', 2: 'warning', 3: 'neutral', 4: 'error',
  5: 'success', 6: 'neutral', 7: 'tertiary', 8: 'warning',
};

export function CatalogoPage() {
  const { data, isLoading } = useCatalogos();

  // Sin la columna "Código" varios tipos comparten el mismo nombre (ej. "Especiales /
  // Internacionales" para E e I) -- se agrupan en una sola fila en vez de mostrar duplicados.
  const tiposAgrupados = useMemo(() => {
    const vistos = new Set<string>();
    return (data?.tiposLicitacion ?? []).filter(t => {
      if (vistos.has(t.nombre)) return false;
      vistos.add(t.nombre);
      return true;
    });
  }, [data?.tiposLicitacion]);

  // US3 (spec 019): se sacaron todas las columnas de "código" interno (numérico o alfabético)
  // -- el usuario pidió sacarlas: no le aportan nada a un usuario de negocio. También se sacó
  // el modal/drawer explicativo al hacer click en una fila: sus descripciones (constants/
  // catalogoDescripciones.ts) usaban claves genéricas que nunca calzaban con los códigos
  // reales -- estaba vacío para los 17 tipos, y en Estados calzaba por accidente con contenido
  // de OTRO estado en 2 casos (ej. código real 5 = "Publicada", pero el texto mostrado era el
  // de "Revocada"). El nombre visible en la tabla ya comunica lo esencial.
  const estadosColumns = [
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
      render: (_: unknown, record: EstadoItem) => (
        <StatusBadge variant={ESTADO_VARIANT[record.codigo] ?? 'neutral'} label={record.nombre} />
      ),
    },
  ];

  const tiposColumns = [
    {
      title: 'Nombre',
      dataIndex: 'nombre',
      key: 'nombre',
      render: (v: string) => <span style={{ fontWeight: 500, fontSize: 13 }}>{v}</span>,
    },
  ];

  const monedasColumns = [
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
          dataSource={tiposAgrupados}
          rowKey="codigo"
          loading={isLoading}
          size="middle"
          pagination={false}
          data-testid="catalogo-tipos-table"
          style={{ borderRadius: 10, overflow: 'hidden' }}
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
      <PageHeader
        icon={<DatabaseOutlined />}
        title="Catálogos"
        subtitle="Referencia de estados, tipos de licitación y monedas del sistema"
        actions={
          <Space size={8}>
            {data?.estadosLicitacion && <StatusBadge variant="success" label={`${data.estadosLicitacion.length} estados`} />}
            {data?.tiposLicitacion && <StatusBadge variant="tertiary" label={`${tiposAgrupados.length} tipos`} />}
            {data?.monedas && <StatusBadge variant="warning" label={`${data.monedas.length} monedas`} />}
          </Space>
        }
      />

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
    </Space>
  );
}
