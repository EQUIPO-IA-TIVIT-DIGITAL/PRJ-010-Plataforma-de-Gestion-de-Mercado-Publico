import { Table, Tag, Tooltip, Button, message } from 'antd';
import { StarOutlined, StarFilled, LoadingOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import type { LicitacionResumen, PaginationInfo } from '../types/licitacion';
import { useEsSeguida, useSeguirToggle } from '../hooks/useLicitaciones';

interface Props {
  dataSource: LicitacionResumen[];
  pagination: PaginationInfo | null;
  loading: boolean;
  onRowClick: (row: LicitacionResumen) => void;
  onPageChange: (page: number, pageSize: number) => void;
  onSortChange: (sortBy: string, sortDir: 'asc' | 'desc') => void;
}

function StarButtonCell({ codigoExterno }: { codigoExterno: string }) {
  const { data: esSeguida, isLoading: loadingState } = useEsSeguida(codigoExterno);
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

const STATUS_CONFIG: Record<number, { color: string; bg: string; label?: string }> = {
  1: { color: '#3b82f6', bg: '#eff6ff' },
  2: { color: '#f59e0b', bg: '#fffbeb' },
  3: { color: '#64748b', bg: '#f8fafc' },
  4: { color: '#ef4444', bg: '#fef2f2' },
  5: { color: '#10b981', bg: '#f0fdf4' },
  6: { color: '#64748b', bg: '#f8fafc' },
  7: { color: '#8b5cf6', bg: '#faf5ff' },
  8: { color: '#f59e0b', bg: '#fffbeb' },
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

export function LicitacionesTable({ dataSource, pagination, loading, onRowClick, onPageChange }: Props) {
  const columns: ColumnsType<LicitacionResumen> = [
    {
      title: 'Código',
      dataIndex: 'codigoExterno',
      key: 'codigo_externo',
      width: 145,
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
      title: 'Estado',
      dataIndex: 'estado',
      key: 'estado',
      width: 135,
      render: (estado: { codigo: number; nombre: string }) => {
        const cfg = STATUS_CONFIG[estado.codigo] ?? { color: '#64748b', bg: '#f8fafc' };
        return (
          <span
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 5,
              padding: '4px 10px',
              borderRadius: 999,
              fontSize: 11,
              fontWeight: 600,
              color: cfg.color,
              background: cfg.bg,
              whiteSpace: 'nowrap',
            }}
          >
            <span
              style={{
                width: 6,
                height: 6,
                borderRadius: '50%',
                background: cfg.color,
                flexShrink: 0,
              }}
            />
            {estado.nombre}
          </span>
        );
      },
    },
    {
      title: 'Tipo',
      dataIndex: 'tipo',
      key: 'tipo',
      width: 125,
      render: (tipo: string) => (
        <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{tipo ?? '—'}</span>
      ),
    },
    {
      title: 'Publicación',
      dataIndex: 'fechaPublicacion',
      key: 'fecha_publicacion',
      width: 110,
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
      key: 'seguir',
      width: 44,
      align: 'center',
      render: (_: unknown, record: LicitacionResumen) => (
        <StarButtonCell codigoExterno={record.codigoExterno} />
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
