import { Alert, Button, List, Space, Spin, Typography, App as AntdApp } from 'antd';
import { DownloadOutlined, ReloadOutlined, FilePdfOutlined } from '@ant-design/icons';
import {
  descargarArchivoDocumento,
  formatTamanio,
  useDescargarDocumentos,
  useEstadoDocumentos,
} from '../hooks/useDocumentosLicitacion';
import type { LicitacionDocumentoItem } from '../types/licitacion';

interface Props {
  codigoExterno: string;
}

export function DocumentosLicitacionPanel({ codigoExterno }: Props) {
  const { message } = AntdApp.useApp();
  const { data: estadoData, isLoading: estadoLoading } = useEstadoDocumentos(codigoExterno);
  const descargarMutation = useDescargarDocumentos();
  const estado = estadoData?.data;

  const iniciarDescarga = () => {
    descargarMutation.mutate(
      { codigoExterno },
      {
        onSuccess: () => message.success('Descarga de documentos iniciada (puede tardar unos minutos)'),
        onError: (e) => message.error(e instanceof Error ? e.message : 'No se pudo iniciar la descarga'),
      },
    );
  };

  const descargarLocal = (doc: LicitacionDocumentoItem) => {
    descargarArchivoDocumento(codigoExterno, doc).catch(() =>
      message.error('No se pudo descargar el archivo'),
    );
  };

  return (
    <div style={{ padding: '8px 0' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <div>
          <Typography.Title level={5} style={{ margin: 0 }}>
            Pliegos y Bases Administrativas / Técnicas
          </Typography.Title>
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            Descarga bajo demanda desde Mercado Público. Los documentos quedan guardados en caché para todo el equipo.
          </Typography.Text>
        </div>
        <Button
          type="primary"
          icon={<DownloadOutlined />}
          loading={descargarMutation.isPending || estado?.estadoConjunto === 'descargando'}
          onClick={iniciarDescarga}
        >
          {estado?.documentos && estado.documentos.length > 0 ? 'Actualizar / Re-descargar' : 'Descargar documentos'}
        </Button>
      </div>

      {estadoLoading && !estado ? (
        <div style={{ textAlign: 'center', padding: 40 }}><Spin tip="Consultando estado de documentos..." /></div>
      ) : !estado || estado.estadoConjunto === 'pendiente' ? (
        <Alert
          type="info"
          showIcon
          message="Los pliegos aún no se han descargado"
          description="Presiona 'Descargar documentos' para extraer las bases y anexos oficiales desde Mercado Público."
        />
      ) : estado.estadoConjunto === 'descargando' ? (
        <Alert
          type="info"
          showIcon
          icon={<ReloadOutlined spin />}
          message="Descargando documentos desde Mercado Público..."
          description="La extracción se realiza en segundo plano. Esta sección se actualizará automáticamente al finalizar."
        />
      ) : estado.estadoConjunto === 'error' ? (
        <Alert
          type="error"
          showIcon
          message="La descarga falló"
          description={estado.descargaError ?? 'Ocurrió un problema al extraer los documentos'}
          action={
            <Button size="small" icon={<ReloadOutlined />} onClick={iniciarDescarga}>
              Reintentar
            </Button>
          }
        />
      ) : (
        <>
          <List
            bordered
            size="small"
            dataSource={estado.documentos}
            renderItem={(doc) => (
              <List.Item
                actions={[
                  <Button
                    key="descargar"
                    type="link"
                    size="small"
                    icon={<DownloadOutlined />}
                    onClick={() => descargarLocal(doc)}
                  >
                    Descargar
                  </Button>,
                ]}
              >
                <List.Item.Meta
                  avatar={<FilePdfOutlined style={{ fontSize: 24, color: '#ff4d4f' }} />}
                  title={<Typography.Text strong>{doc.nombreArchivo}</Typography.Text>}
                  description={
                    <Space size={16} wrap>
                      <span>{doc.esActa ? 'Acta de evaluación' : 'Bases / Anexo'}</span>
                      <span>Tamaño: {formatTamanio(doc.tamanioBytes)}</span>
                      <span>Versión: v{doc.version}</span>
                      {doc.fechaGrilla && <span>Portal: {doc.fechaGrilla}</span>}
                    </Space>
                  }
                />
              </List.Item>
            )}
          />
          {estado.conjuntoHash && (
            <div style={{ marginTop: 12 }}>
              <Typography.Text orientation="left" type="secondary" style={{ fontSize: 12 }}>
                ✓ Caché activo (Hash SHA-256: <code>{estado.conjuntoHash.slice(0, 20)}…</code>). Los archivos están respaldados de forma inmutable.
              </Typography.Text>
            </div>
          )}
        </>
      )}
    </div>
  );
}
