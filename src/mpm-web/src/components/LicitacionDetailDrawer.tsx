import { Drawer, Descriptions, Table, Typography, Empty, Spin, Button, Space, Card } from 'antd';
import { RocketOutlined, GlobalOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import type { LicitacionDetalle } from '../types/licitacion';
import { LicitacionInteresPanel } from './LicitacionInteresPanel';
import { StatusBadge } from './StatusBadge';
import type { StatusBadgeVariant } from './StatusBadge';

// US1 (spec 019): mismo mapeo que LicitacionesTable.tsx (ESTADO_VARIANT) -- via StatusBadge.
const ESTADO_VARIANT: Record<number, StatusBadgeVariant> = {
  1: 'info', 2: 'warning', 3: 'neutral', 4: 'error',
  5: 'success', 6: 'neutral', 7: 'tertiary', 8: 'warning',
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
  const navigate = useNavigate();

  const irASalaOferta = () => {
    if (!data) return;
    onClose();
    navigate(`/licitaciones/${encodeURIComponent(data.codigoExterno)}/oferta`);
  };

  return (
    <Drawer
      title={data ? `Licitación ${data.codigoExterno}` : 'Detalle'}
      placement="right"
      width={640}
      open={open}
      onClose={onClose}
      data-testid="licitacion-drawer"
      extra={
        data ? (
          <Button
            type="primary"
            icon={<RocketOutlined />}
            onClick={irASalaOferta}
            data-testid="btn-abrir-sala-oferta"
          >
            Abrir Sala de Oferta
          </Button>
        ) : undefined
      }
    >
      {loading && !data ? (
        <div style={{ textAlign: 'center', padding: 40 }}>
          <Spin size="large" tip="Cargando detalle desde API Mercado Público..." />
        </div>
      ) : !data ? (
        <Empty description="Seleccione una licitación" />
      ) : (
        <>
          <Descriptions column={2} size="small" bordered>
            <Descriptions.Item label="Código" span={2}>
              <Typography.Text copyable>{data.codigoExterno}</Typography.Text>
            </Descriptions.Item>
            <Descriptions.Item label="Nombre" span={2}>{data.nombre}</Descriptions.Item>
            <Descriptions.Item label="Estado">
              <StatusBadge variant={ESTADO_VARIANT[data.estado?.codigo] ?? 'neutral'} label={data.estado?.nombre} />
            </Descriptions.Item>
            <Descriptions.Item label="Tipo">{data.tipo}</Descriptions.Item>
            <Descriptions.Item label="Organismo" span={2}>{data.organismo}</Descriptions.Item>
            {data.unidadTecnica && (
              <Descriptions.Item label="Unidad Técnica" span={2}>{data.unidadTecnica}</Descriptions.Item>
            )}
            <Descriptions.Item label="Publicación">{formatDate(data.fechaPublicacion)}</Descriptions.Item>
            <Descriptions.Item label="Cierre">{formatDate(data.fechaCierre)}</Descriptions.Item>
            <Descriptions.Item label="Moneda">{data.moneda}</Descriptions.Item>
            <Descriptions.Item label="Monto Estimado">{formatCurrency(data.montoEstimado)}</Descriptions.Item>
            {data.link && (
              <Descriptions.Item label="Link Oficial" span={2}>
                <Button
                  type="link"
                  size="small"
                  icon={<GlobalOutlined />}
                  href={data.link}
                  target="_blank"
                  rel="noopener noreferrer"
                  style={{ padding: 0 }}
                >
                  Ver en portal Mercado Público
                </Button>
              </Descriptions.Item>
            )}
          </Descriptions>

          {/* CTA Box to Full-screen Commercial Offer Room */}
          <Card
            size="small"
            style={{
              marginTop: 20,
              background: 'linear-gradient(135deg, rgba(230, 247, 255, 0.6) 0%, rgba(246, 255, 237, 0.6) 100%)',
              borderColor: '#91caff',
            }}
          >
            <Space direction="vertical" size={10} style={{ width: '100%' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <RocketOutlined style={{ fontSize: 18, color: '#1677ff' }} />
                <Typography.Text strong style={{ fontSize: 14 }}>
                  Sala de Oferta y Análisis con IA
                </Typography.Text>
              </div>
              <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                Descarga de pliegos, análisis comercial automático, match de talentos Census, comité GO/NO GO y generación de propuestas DOCX.
              </Typography.Text>
              <Button
                type="primary"
                icon={<RocketOutlined />}
                block
                onClick={irASalaOferta}
                data-testid="btn-cta-sala-oferta"
              >
                Entrar a la Sala de Oferta
              </Button>
            </Space>
          </Card>

          {/* Colaboración e interés rápido */}
          <Typography.Title level={5} style={{ marginTop: 24 }}>
            Interés y colaboración
          </Typography.Title>
          <LicitacionInteresPanel licitacionId={data.id} licitacionNombre={data.nombre} />

          {/* Items de la licitación */}
          {data.items && data.items.length > 0 && (
            <>
              <Typography.Title level={5} style={{ marginTop: 24 }}>
                Ítems ({data.items.length})
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
