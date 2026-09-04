import { Table, Tooltip, Button, message, Typography } from 'antd';
import { StarOutlined, StarFilled, LoadingOutlined, RocketOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import type { ColumnsType } from 'antd/es/table';
import type { LicitacionResumen, PaginationInfo } from '../types/licitacion';
import { useEsSeguida, useSeguirToggle } from '../hooks/useLicitaciones';
import { useMediaQuery } from '../hooks/useMediaQuery';
import { StatusBadge } from './StatusBadge';
import type { StatusBadgeVariant } from './StatusBadge';

interface Props {
  dataSource: LicitacionResumen[];
  pagination: PaginationInfo | null;
  loading: boolean;
  onRowClick: (row: LicitacionResumen) => void;
  onPageChange: (page: number, pageSize: number) => void;
  onSortChange: (sortBy: string, sortDir: 'asc' | 'desc') => void;
  seguidasSet?: Set<string>;
}

function StarButtonCell({ codigoExterno, seguidasSet }: { codigoExterno: string; seguidasSet?: Set<string> }) {
  const singleQuery = useEsSeguida(seguidasSet ? '' : codigoExterno);
  const esSeguidaFromSet = seguidasSet ? seguidasSet.has(codigoExterno) : undefined;
  const esSeguida = seguidasSet ? esSeguidaFromSet : singleQuery.data;
  const loadingState = seguidasSet ? false : singleQuery.isLoading;
  const toggle = useSeguirToggle();

  const handleClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    toggle.mutate(codigoExterno, {
      onSuccess: (result) => {
        message.success(result.accion === 'seguida' ? 'Siguiendo licitación' : 'Dejaste de seguir');
      },
      onError: () => message.error('Error al actualizar seguimiento'),
    });
  };

  if (loadingState) return <LoadingOutlined style={{ color: '#94a3b8', fontSize: 16 }} />;

  return (
    <Tooltip title={esSeguida ? 'Dejar de seguir' : 'Seguir licitación'}>
      <Button
        type="text"
        size="small"
        icon={esSeguida
          ? <StarFilled style={{ color: '#f59e0b', fontSize: 17 }} />
          : <StarOutlined style={{ color: '#94a3b8', fontSize: 17 }} />
        }
        loading={toggle.isPending}
        onClick={handleClick}
        style={{ padding: '2px 4px', lineHeight: 1 }}
      />
    </Tooltip>
  );
}

// US1 (spec 019): mismo mapeo de color que el STATUS_CONFIG anterior, ahora a traves de
// StatusBadge (6 variantes del sistema) en vez de un hex propio por pantalla.
const ESTADO_VARIANT: Record<number, StatusBadgeVariant> = {
  1: 'info',
  2: 'warning',
  3: 'neutral',
  4: 'error',
  5: 'success',
  6: 'neutral',
  7: 'tertiary',
  8: 'warning',
};

function formatDate(d: string | null): string {
  if (!d) return '—';
  return new Intl.DateTimeFormat('es-CL', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(new Date(d));
}

function isClosingSoon(fechaCierre: string | null): boolean {
  if (!fechaCierre) return false;
  const diff = new Date(fechaCierre).getTime() - Date.now();
  return diff > 0 && diff < 3 * 24 * 60 * 60 * 1000; // 3 días
}

const formatCLP = (value: number | null | undefined): string => {
  if (value === null || value === undefined) return '—';
  return new Intl.NumberFormat('es-CL', {
    style: 'currency',
    currency: 'CLP',
    minimumFractionDigits: 0,
  }).format(value);
};

function formatMonto(monto: number | null, moneda: string): string {
  if (monto == null) return '—';
  try {
    const f = new Intl.NumberFormat('es-CL', { style: 'currency', currency: moneda || 'CLP', maximumFractionDigits: 0 });
    return f.format(monto);
  } catch {
    return `${moneda || 'CLP'} ${monto.toLocaleString('es-CL')}`;
  }
}

export function LicitacionesTable({ dataSource, pagination, loading, onRowClick, onPageChange, onSortChange, seguidasSet }: Props) {
  const navigate = useNavigate();
  const isMobile = useMediaQuery('(max-width: 639px)');

  const handleTableChange = (_pagination: any, _filters: any, sorter: any) => {
    if (sorter && sorter.field && sorter.order) {
      const sortBy = sorter.field;
      const sortDir = sorter.order === 'ascend' ? 'asc' : 'desc';
      onSortChange(sortBy, sortDir);
    }
  };

  const columns: ColumnsType<LicitacionResumen> = [
    {
      title: 'Código',
      dataIndex: 'codigoExterno',
      key: 'codigo_externo',
      width: 145,
      sorter: (a, b) => a.codigoExterno.localeCompare(b.codigoExterno),
      render: (codigo: string) => (
        <span
          style={{
            fontFamily: 'var(--font-mono, monospace)',
            fontSize: 12,
            fontWeight: 600,
            color: '#3b82f6',
            background: '#eff6ff',
            padding: '3px 8px',
            borderRadius: 6,
            letterSpacing: '0.02em',
          }}
        >
          {codigo}
        </span>
      ),
    },
    {
      title: 'Nombre',
      dataIndex: 'nombre',
      key: 'nombre',
      ellipsis: { showTitle: false },
      sorter: (a, b) => a.nombre.localeCompare(b.nombre),
      render: (nombre: string) => (
        <Tooltip title={nombre} placement="topLeft">
          <span
            style={{
              fontWeight: 500,
              color: 'var(--text-primary)',
              fontSize: 13,
            }}
          >
            {nombre}
          </span>
        </Tooltip>
      ),
    },
    {
      title: 'Institución',
      dataIndex: 'organismo',
      key: 'organismo',
      width: 180,
      sorter: (a, b) => (a.organismo || '').localeCompare(b.organismo || ''),
      render: (organismo: string) => (
        <Typography.Text
          ellipsis={{
            tooltip: organismo ? `Institución: ${organismo}` : 'Institución: —',
          }}
          style={{ display: 'block', maxWidth: '100%', fontSize: 12, color: 'var(--text-secondary)' }}
        >
          {organismo ? `Institución: ${organismo}` : 'Institución: —'}
        </Typography.Text>
      ),
    },
    {
      title: 'Presupuesto',
      dataIndex: 'montoEstimado',
      key: 'monto_estimado',
      width: 140,
      align: 'right',
      sorter: (a, b) => {
        const aVal = a.montoEstimado ?? -1;
        const bVal = b.montoEstimado ?? -1;
        return aVal - bVal;
      },
      sortDirections: ['descend', 'ascend'],
      render: (monto: number | null, record: LicitacionResumen) => {
        const formatted = formatCLP(monto);
        return (
          <span style={{ fontSize: 12, fontWeight: monto != null ? 600 : 400, color: monto != null ? 'var(--text-primary)' : 'var(--text-secondary)', whiteSpace: 'nowrap' }}>
            {isMobile ? formatted : `Presupuesto: ${formatted}`}
          </span>
        );
      },
    },
    {
      title: 'Estado',
      dataIndex: 'estado',
      key: 'estado',
      width: 135,
      sorter: (a, b) => a.estado.codigo - b.estado.codigo,
      render: (estado: { codigo: number; nombre: string }) => (
        <StatusBadge variant={ESTADO_VARIANT[estado.codigo] ?? 'neutral'} label={estado.nombre} />
      ),
    },
    {
      title: 'Tipo',
      dataIndex: 'tipo',
      key: 'tipo',
      width: 125,
      sorter: (a, b) => (a.tipo || '').localeCompare(b.tipo || ''),
      render: (tipo: string) => (
        <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{tipo ?? '—'}</span>
      ),
    },
    {
      title: 'Publicación',
      dataIndex: 'fechaPublicacion',
      key: 'fecha_publicacion',
      width: 110,
      sorter: (a, b) => new Date(a.fechaPublicacion || 0).getTime() - new Date(b.fechaPublicacion || 0).getTime(),
      render: (d: string) => (
        <span style={{ fontSize: 12, color: 'var(--text-secondary)', whiteSpace: 'nowrap' }}>
          {formatDate(d)}
        </span>
      ),
    },
    {
      title: 'Cierre',
      dataIndex: 'fechaCierre',
      key: 'fecha_cierre',
      width: 110,
      sorter: (a, b) => new Date(a.fechaCierre || 0).getTime() - new Date(b.fechaCierre || 0).getTime(),
      render: (d: string) => {
        const soon = isClosingSoon(d);
        return (
          <span
            style={{
              fontSize: 12,
              color: soon ? '#ef4444' : 'var(--text-secondary)',
              fontWeight: soon ? 600 : 400,
              whiteSpace: 'nowrap',
            }}
          >
            {soon && '⚡ '}
            {formatDate(d)}
          </span>
        );
      },
    },
    {
      title: '',
      key: 'oferta',
      width: 44,
      align: 'center',
      render: (_: unknown, record: LicitacionResumen) => (
        <Tooltip title="Abrir Sala de Oferta y Análisis IA">
          <Button
            type="text"
            size="small"
            icon={<RocketOutlined style={{ color: '#1677ff', fontSize: 14 }} />}
            onClick={(e) => {
              e.stopPropagation();
              navigate(`/licitaciones/${encodeURIComponent(record.codigoExterno)}/oferta`);
            }}
            data-testid={`btn-sala-oferta-${record.codigoExterno}`}
          />
        </Tooltip>
      ),
    },
    {
      title: '',
      key: 'seguir',
      width: 44,
      align: 'center',
      render: (_: unknown, record: LicitacionResumen) => (
        <StarButtonCell codigoExterno={record.codigoExterno} seguidasSet={seguidasSet} />
      ),
    },
  ];

  return (
    <div data-testid="licitaciones-table">
      <Table<LicitacionResumen>
        columns={columns}
        dataSource={dataSource}
        rowKey="codigoExterno"
        loading={loading}
        size="small"
        scroll={{ x: 1100 }}
        onChange={handleTableChange}
        onRow={(record) => ({
          onClick: () => onRowClick(record),
          style: { cursor: 'pointer' },
        })}
        style={{
          borderRadius: 14,
          overflow: 'hidden',
          boxShadow: 'var(--shadow-card)',
          background: 'white',
        }}
        pagination={{
          current: pagination?.page ?? 1,
          pageSize: pagination?.pageSize ?? 20,
          total: pagination?.totalRecords ?? 0,
          showSizeChanger: true,
          pageSizeOptions: ['10', '20', '50', '100'],
          showTotal: (total, range) =>
            `Mostrando ${range[0]}–${range[1]} de ${total.toLocaleString('es-CL')} licitaciones`,
          onChange: (page, pageSize) => onPageChange(page, pageSize),
          style: { padding: '12px 16px' },
        }}
      />
    </div>
  );
}
