import { Drawer, Descriptions, Table, Tag, Typography, Empty, Spin } from 'antd';
import type { LicitacionDetalle } from '../types/licitacion';

const STATUS_COLORS: Record<number, string> = {
  1: 'blue', 2: 'orange', 3: 'default', 4: 'red',
  5: 'green', 6: 'default', 7: 'purple', 8: 'gold',
};

function formatDate(d: string | null): string {
  if (!d) return '-';
  return new Intl.DateTimeFormat('es-CL', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(d));
}

function formatCurrency(v: number | null): string {
  if (v == null) return '-';
  return new Intl.NumberFormat('es-CL', { style: 'currency', currency: 'CLP', maximumFractionDigits: 0 }).format(v);
}

interface Props {
  open: boolean;
  data: LicitacionDetalle | null;
  loading: boolean;
  onClose: () => void;
}

export function LicitacionDetailDrawer({ open, data, loading, onClose }: Props) {
  return (
    <Drawer
      title={data ? `Licitacion ${data.codigoExterno}` : 'Detalle'}
      placement="right"
      width={640}
      open={open}
      onClose={onClose}
      data-testid="licitacion-drawer"
    >
      {loading && !data ? (
        <div style={{ textAlign: 'center', padding: 40 }}>
          <Spin size="large" tip="Cargando detalle desde API Mercado Publico..." />
        </div>
      ) : !data ? (
        <Empty description="Seleccione una licitacion" />
      ) : (
        <>
          <Descriptions column={2} size="small" bordered>
            <Descriptions.Item label="Codigo" span={2}>
              <Typography.Text copyable>{data.codigoExterno}</Typography.Text>
            </Descriptions.Item>
            <Descriptions.Item label="Nombre" span={2}>{data.nombre}</Descriptions.Item>
            <Descriptions.Item label="Estado">
              <Tag color={STATUS_COLORS[data.estado?.codigo] ?? 'default'}>
                {data.estado?.nombre}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label="Tipo">{data.tipo}</Descriptions.Item>
            <Descriptions.Item label="Organismo" span={2}>{data.organismo}</Descriptions.Item>
            {data.unidadTecnica && (
              <Descriptions.Item label="Unidad Tecnica" span={2}>{data.unidadTecnica}</Descriptions.Item>
            )}
            <Descriptions.Item label="Publicacion">{formatDate(data.fechaPublicacion)}</Descriptions.Item>
            <Descriptions.Item label="Cierre">{formatDate(data.fechaCierre)}</Descriptions.Item>
            <Descriptions.Item label="Moneda">{data.moneda}</Descriptions.Item>
            <Descriptions.Item label="Monto Estimado">{formatCurrency(data.montoEstimado)}</Descriptions.Item>
            {data.link && (
              <Descriptions.Item label="Link" span={2}>
                <a href={data.link} target="_blank" rel="noopener noreferrer">
                  Ver en Mercado Publico
                </a>
              </Descriptions.Item>
            )}
          </Descriptions>

          {data.items && data.items.length > 0 && (
            <>
              <Typography.Title level={5} style={{ marginTop: 24 }}>
                Items ({data.items.length})
              </Typography.Title>
              <Table
                dataSource={data.items}
                rowKey="codigo"
                size="small"
                pagination={false}
                columns={[
                  { title: '#', dataIndex: 'codigo', width: 40 },
                  { title: 'Nombre', dataIndex: 'nombre', ellipsis: true },
                  { title: 'Cant.', dataIndex: 'cantidad', width: 60 },
                  { title: 'Unidad', dataIndex: 'unidadMedida', width: 70 },
                  { title: 'Precio', dataIndex: 'precioEstimado', width: 100, render: (v: number) => formatCurrency(v) },
                ]}
              />
            </>
          )}
        </>
      )}
    </Drawer>
  );
}
