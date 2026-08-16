import { Drawer, Descriptions, Table, Typography, Empty, Spin, Button, Alert, List, Space, App as AntdApp } from 'antd';
import { DownloadOutlined, ReloadOutlined, RobotOutlined } from '@ant-design/icons';
import type { LicitacionDetalle } from '../types/licitacion';
import { LicitacionInteresPanel } from './LicitacionInteresPanel';
import { AnalisisComercialPanel } from './AnalisisComercialPanel';
import { CapacidadesTIVITPanel } from './CapacidadesTIVITPanel';
import { DecisionGoNoGoPanel } from './DecisionGoNoGoPanel';
import { StatusBadge } from './StatusBadge';
import type { StatusBadgeVariant } from './StatusBadge';
import { descargarArchivoDocumento, formatTamanio, useDescargarDocumentos, useEstadoDocumentos } from '../hooks/useDocumentosLicitacion';
import { useIniciarAnalisisComercial } from '../hooks/useAnalisisComercial';

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
  const { message } = AntdApp.useApp();
  const codigoExterno = open && data ? data.codigoExterno : null;
  const { data: estadoData, isLoading: estadoLoading } = useEstadoDocumentos(codigoExterno);
  const descargar = useDescargarDocumentos();
  const iniciarAnalisis = useIniciarAnalisisComercial();
  const estado = estadoData?.data;
  const documentosListos = estado?.estadoConjunto === 'completado' && (estado.documentos.length ?? 0) > 0;

  const iniciarDescarga = () => {
    if (!data) return;
    descargar.mutate(
      { codigoExterno: data.codigoExterno },
      {
        onSuccess: () => message.success('Descarga de documentos iniciada (puede tardar unos minutos)'),
        onError: (e) => message.error(e instanceof Error ? e.message : 'No se pudo iniciar la descarga'),
      },
    );
  };

  const iniciarAnalisisIa = () => {
    if (!data) return;
    iniciarAnalisis.mutate(data.codigoExterno, {
      onSuccess: (r) => {
        if (r.data.cacheHit) message.info('Análisis recuperado de cache (los documentos no cambiaron)');
        else message.success('Análisis con IA iniciado (puede tardar unos minutos)');
      },
      onError: (e) => message.error(e instanceof Error ? e.message : 'No se pudo iniciar el análisis'),
    });
  };

  return (
    <Drawer
      title={data ? `Licitacion ${data.codigoExterno}` : 'Detalle'}
      placement="right"
      width={640}
      open={open}
      onClose={onClose}
      data-testid="licitacion-drawer"
      extra={
        data ? (
          <Space>
            <Button
              icon={<RobotOutlined />}
              loading={iniciarAnalisis.isPending}
              disabled={!documentosListos}
              onClick={iniciarAnalisisIa}
              data-testid="btn-analizar-ia"
            >
              Analizar con IA
            </Button>
            <Button
              type="primary"
              icon={<DownloadOutlined />}
              loading={descargar.isPending}
              disabled={estado?.estadoConjunto === 'descargando'}
              onClick={iniciarDescarga}
              data-testid="btn-descargar-documentos"
            >
              Descargar documentos
            </Button>
          </Space>
        ) : undefined
      }
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
              <StatusBadge variant={ESTADO_VARIANT[data.estado?.codigo] ?? 'neutral'} label={data.estado?.nombre} />
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

          {/* 036-flujo-comercial-ofertas (Fase 1): documentos de la licitación */}
          <Typography.Title level={5} style={{ marginTop: 24 }}>
            Documentos de la licitacion
          </Typography.Title>
          {estadoLoading && !estado ? (
            <Spin />
          ) : !estado || estado.estadoConjunto === 'pendiente' ? (
            <Alert
              type="info"
              showIcon
              message="Los pliegos aun no se han descargado"
              description="Usa el boton 'Descargar documentos' para traerlos desde Mercado Publico (una sola vez: quedan en cache para todo el equipo)."
            />
          ) : estado.estadoConjunto === 'descargando' ? (
            <Alert
              type="info"
              showIcon
              message="Descargando documentos..."
              description="La extraccion puede tardar unos minutos (sesion + cupo de 'Ver Adjuntos'). Esta seccion se actualiza sola."
            />
          ) : estado.estadoConjunto === 'error' ? (
            <Alert
              type="error"
              showIcon
              message="La descarga fallo"
              description={estado.descargaError ?? 'Intente nuevamente'}
              action={
                <Button size="small" icon={<ReloadOutlined />} onClick={iniciarDescarga}>
                  Reintentar
                </Button>
              }
            />
          ) : (
            <>
              <List
                size="small"
                dataSource={estado.documentos}
                renderItem={(doc) => (
                  <List.Item
                    actions={[
                      <Button
                        key="descargar"
                        type="link"
                        size="small"
                        onClick={() =>
                          descargarArchivoDocumento(data.codigoExterno, doc).catch(() =>
                            message.error('No se pudo descargar el archivo'),
                          )
                        }
                      >
                        Descargar
                      </Button>,
                    ]}
                  >
                    <List.Item.Meta
                      title={doc.nombreArchivo}
                      description={`${doc.esActa ? 'Acta de evaluacion · ' : ''}${formatTamanio(doc.tamanioBytes)} · v${doc.version}${doc.fechaGrilla ? ` · portal: ${doc.fechaGrilla}` : ''}`}
                    />
                  </List.Item>
                )}
              />
              {estado.conjuntoHash && (
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  Cache activo ({estado.conjuntoHash.slice(0, 16)}…): los documentos ya estan guardados para todo el equipo.
                </Typography.Text>
              )}
            </>
          )}

          {/* 036-flujo-comercial-ofertas (Fase 1.3): zona IA on-demand */}
          <AnalisisComercialPanel codigoExterno={data.codigoExterno} />

          {/* 036-flujo-comercial-ofertas (Fase 2): match de capacidades TIVIT + decisión GO/NO GO */}
          <CapacidadesTIVITPanel codigoExterno={data.codigoExterno} />
          <DecisionGoNoGoPanel codigoExterno={data.codigoExterno} />

          {/* spec 031 (US5): flujo colaborativo go/no-go */}
          <Typography.Title level={5} style={{ marginTop: 24 }}>
            Interes y colaboracion
          </Typography.Title>
          <LicitacionInteresPanel licitacionId={data.id} licitacionNombre={data.nombre} />

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
