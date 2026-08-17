import { useState, useEffect } from 'react';
import {
  Alert,
  Button,
  Card,
  List,
  Popconfirm,
  Progress,
  Space,
  Spin,
  Tag,
  Typography,
  App as AntdApp,
} from 'antd';
import {
  DownloadOutlined,
  ReloadOutlined,
  FilePdfOutlined,
  FileWordOutlined,
  FileExcelOutlined,
  FileZipOutlined,
  FileTextOutlined,
  CheckCircleOutlined,
  RobotOutlined,
  SyncOutlined,
} from '@ant-design/icons';
import {
  descargarArchivoDocumento,
  formatTamanio,
  useDescargarDocumentos,
  useEstadoDocumentos,
} from '../hooks/useDocumentosLicitacion';
import type { LicitacionDocumentoItem } from '../types/licitacion';

interface Props {
  codigoExterno: string;
  onIrAAnalisis?: () => void;
}

function getFileIcon(nombre: string) {
  const ext = nombre.split('.').pop()?.toLowerCase();
  if (ext === 'pdf') return <FilePdfOutlined style={{ fontSize: 24, color: '#ff4d4f' }} />;
  if (ext === 'doc' || ext === 'docx') return <FileWordOutlined style={{ fontSize: 24, color: '#1677ff' }} />;
  if (ext === 'xls' || ext === 'xlsx') return <FileExcelOutlined style={{ fontSize: 24, color: '#52c41a' }} />;
  if (ext === 'zip' || ext === 'rar') return <FileZipOutlined style={{ fontSize: 24, color: '#faad14' }} />;
  return <FileTextOutlined style={{ fontSize: 24, color: '#8c8c8c' }} />;
}

export function DocumentosLicitacionPanel({ codigoExterno, onIrAAnalisis }: Props) {
  const { message } = AntdApp.useApp();
  const { data: estadoData, isLoading: estadoLoading } = useEstadoDocumentos(codigoExterno);
  const descargarMutation = useDescargarDocumentos();
  const estado = estadoData?.data;

  // Contador de segundos en vivo durante la extracción
  const [segundosTranscurridos, setSegundosTranscurridos] = useState(0);

  const enProgreso = estado?.estadoConjunto === 'descargando' || descargarMutation.isPending;
  const completado = estado?.estadoConjunto === 'completado' && (estado.documentos?.length ?? 0) > 0;

  useEffect(() => {
    let timer: any;
    if (enProgreso) {
      timer = setInterval(() => setSegundosTranscurridos((prev) => prev + 1), 1000);
    } else {
      setSegundosTranscurridos(0);
    }
    return () => clearInterval(timer);
  }, [enProgreso]);

  const iniciarDescarga = (forzar = false) => {
    descargarMutation.mutate(
      { codigoExterno, forzar },
      {
        onSuccess: () => {
          message.info('Extracción iniciada en segundo plano con Playwright');
        },
        onError: (e) => {
          message.error(e instanceof Error ? e.message : 'No se pudo iniciar la descarga');
        },
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
      {/* Header bar */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20, flexWrap: 'wrap', gap: 12 }}>
        <div>
          <Typography.Title level={5} style={{ margin: 0 }}>
            Pliegos y Bases Oficiales
            {completado && (
              <Tag color="success" style={{ marginLeft: 8 }}>
                {estado.documentos.length} archivos descargados
              </Tag>
            )}
          </Typography.Title>
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            Descarga bajo demanda y extracción automatizada desde Mercado Público sin colisiones de sesión.
          </Typography.Text>
        </div>

        <Space size={8}>
          {completado && onIrAAnalisis && (
            <Button type="primary" icon={<RobotOutlined />} onClick={onIrAAnalisis}>
              Pasar al Análisis IA
            </Button>
          )}

          {completado ? (
            <Popconfirm
              title="¿Volver a descargar pliegos?"
              description="Los archivos ya se encuentran descargados. ¿Deseas forzar una nueva extracción desde el portal?"
              onConfirm={() => iniciarDescarga(true)}
              okText="Sí, re-descargar"
              cancelText="Cancelar"
            >
              <Button icon={<ReloadOutlined />} loading={enProgreso}>
                Re-descargar bases
              </Button>
            </Popconfirm>
          ) : (
            <Button
              type="primary"
              icon={<DownloadOutlined />}
              loading={enProgreso}
              onClick={() => iniciarDescarga(false)}
            >
              Descargar documentos
            </Button>
          )}
        </Space>
      </div>

      {/* Estados dinámicos */}
      {estadoLoading && !estado ? (
        <div style={{ textAlign: 'center', padding: 40 }}>
          <Spin tip="Consultando estado de documentos..." />
        </div>
      ) : enProgreso ? (
        /* Tarjeta de progreso dinámico en vivo */
        <Card
          style={{
            marginBottom: 20,
            background: 'linear-gradient(135deg, rgba(230, 247, 255, 0.8) 0%, rgba(240, 245, 255, 0.8) 100%)',
            borderColor: '#91caff',
          }}
        >
          <Space orientation="vertical" size={12} style={{ width: '100%' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <Space>
                <SyncOutlined spin style={{ fontSize: 20, color: '#1677ff' }} />
                <Typography.Text strong style={{ fontSize: 15 }}>
                  Descarga y extracción en curso desde Mercado Público
                </Typography.Text>
              </Space>
              <Tag color="processing">Tiempo transcurrido: {segundosTranscurridos}s</Tag>
            </div>

            <Progress percent={99} status="active" showInfo={false} />

            <Typography.Text type="secondary" style={{ fontSize: 13 }}>
              El agente Playwright está autenticando en el portal, superando la ventana de adjuntos y descargando las bases en segundo plano. Esta sección se actualizará sola automáticamente en cuanto finalice.
            </Typography.Text>
          </Space>
        </Card>
      ) : estado?.estadoConjunto === 'error' ? (
        <Alert
          type="error"
          showIcon
          message="La extracción de documentos falló"
          description={estado.descargaError ?? 'Ocurrió un problema al extraer los documentos desde el portal'}
          action={
            <Button size="small" icon={<ReloadOutlined />} onClick={() => iniciarDescarga(true)}>
              Reintentar
            </Button>
          }
          style={{ marginBottom: 16 }}
        />
      ) : !completado ? (
        <Alert
          type="info"
          showIcon
          message="Los pliegos aún no se han descargado"
          description="Presiona 'Descargar documentos' para iniciar la extracción oficial de las bases y anexos de esta licitación."
          action={
            <Button type="primary" size="small" icon={<DownloadOutlined />} onClick={() => iniciarDescarga(false)}>
              Descargar ahora
            </Button>
          }
        />
      ) : (
        /* Estado completado: Lista limpia y categorizada de documentos */
        <>
          <Alert
            type="success"
            showIcon
            icon={<CheckCircleOutlined />}
            message={`Todos los documentos están disponibles (${estado.documentos.length} archivos)`}
            description="Las bases y anexos han sido verificados y almacenados con hash SHA-256. Ya puedes proceder con el análisis inteligente."
            style={{ marginBottom: 16 }}
          />

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
                  avatar={getFileIcon(doc.nombreArchivo)}
                  title={<Typography.Text strong>{doc.nombreArchivo}</Typography.Text>}
                  description={
                    <Space size={16} wrap>
                      <Tag color={doc.esActa ? 'purple' : 'blue'}>
                        {doc.esActa ? 'Acta de evaluación' : 'Bases / Anexo'}
                      </Tag>
                      <span style={{ color: '#595959' }}>Tamaño: {formatTamanio(doc.tamanioBytes)}</span>
                      <span style={{ color: '#595959' }}>Versión: v{doc.version}</span>
                      {doc.fechaGrilla && <span style={{ color: '#8c8c8c' }}>Portal: {doc.fechaGrilla}</span>}
                    </Space>
                  }
                />
              </List.Item>
            )}
          />

          {estado.conjuntoHash && (
            <div style={{ marginTop: 14 }}>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                ✓ Respaldo inmutable: Hash SHA-256 del conjunto <code>{estado.conjuntoHash.slice(0, 24)}…</code>
              </Typography.Text>
            </div>
          )}
        </>
      )}
    </div>
  );
}
