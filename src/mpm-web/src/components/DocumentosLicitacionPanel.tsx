import { useState, useEffect, useRef } from 'react';
import {
  Alert,
  Button,
  Card,
  List,
  Popconfirm,
  Progress,
  Space,
  Spin,
  Steps,
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
  GlobalOutlined,
  SafetyCertificateOutlined,
  CloudDownloadOutlined,
  InboxOutlined,
  InfoCircleOutlined,
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
  const { data: estadoData, isLoading: estadoLoading, refetch } = useEstadoDocumentos(codigoExterno);
  const descargarMutation = useDescargarDocumentos();
  const estado = estadoData?.data;

  // Contador de segundos en vivo durante la extracción
  const [segundosTranscurridos, setSegundosTranscurridos] = useState(0);
  const prevEnProgresoRef = useRef(false);

  const enProgreso = estado?.estadoConjunto === 'descargando' || descargarMutation.isPending;
  const tieneDocumentos = estado?.estadoConjunto === 'completado' && (estado.documentos?.length ?? 0) > 0;
  const esCompletadoSinDocs = estado?.estadoConjunto === 'completado' && (estado.documentos?.length ?? 0) === 0;

  useEffect(() => {
    let timer: ReturnType<typeof setInterval> | undefined;
    if (enProgreso) {
      timer = setInterval(() => {
        setSegundosTranscurridos((prev) => prev + 1);
        void refetch();
      }, 1500);
    } else {
      // Si acabamos de terminar una descarga activa
      if (prevEnProgresoRef.current && estado) {
        if (estado.estadoConjunto === 'completado') {
          if ((estado.documentos?.length ?? 0) > 0) {
            message.success(`¡Extracción completada! Se obtuvieron ${estado.documentos.length} documentos oficiales.`);
          } else {
            message.info('Extracción finalizada: Esta licitación no registra documentos adjuntos en el portal.');
          }
        } else if (estado.estadoConjunto === 'error') {
          message.error(estado.descargaError || 'La extracción de documentos finalizó con errores.');
        }
      }
      setSegundosTranscurridos(0);
    }

    prevEnProgresoRef.current = enProgreso;
    return () => {
      if (timer) clearInterval(timer);
    };
  }, [enProgreso, refetch, estado, message]);

  const iniciarDescarga = (forzar = false) => {
    setSegundosTranscurridos(1);
    descargarMutation.mutate(
      { codigoExterno, forzar },
      {
        onSuccess: () => {
          message.info('Iniciando extracción automatizada desde Mercado Público...');
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

  // Cálculo de paso actual y porcentaje simulado para la UI
  const pasoActual = segundosTranscurridos < 5 ? 0 : segundosTranscurridos < 13 ? 1 : segundosTranscurridos < 23 ? 2 : 3;
  const porcentajeProgreso = Math.min(96, Math.max(12, segundosTranscurridos * 4));

  const descripcionesPaso = [
    'Inicializando agente navegador y preparando conexión segura con Mercado Público...',
    'Navegando a la ficha oficial de la licitación y validando controles de acceso...',
    'Explorando la grilla de documentos oficiales, actas de evaluación y anexos...',
    'Descargando archivos, verificando firmas de integridad (SHA-256) y sincronizando storage...',
  ];

  return (
    <div style={{ padding: '8px 0' }}>
      {/* Header bar */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20, flexWrap: 'wrap', gap: 12 }}>
        <div>
          <Typography.Title level={5} style={{ margin: 0 }}>
            Pliegos y Bases Oficiales
            {tieneDocumentos && (
              <Tag color="success" style={{ marginLeft: 8 }}>
                {estado.documentos.length} archivos descargados
              </Tag>
            )}
            {esCompletadoSinDocs && (
              <Tag color="default" style={{ marginLeft: 8 }}>
                Sin adjuntos en portal
              </Tag>
            )}
          </Typography.Title>
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            Descarga bajo demanda y extracción automatizada desde Mercado Público.
          </Typography.Text>
        </div>

        <Space size={8}>
          {tieneDocumentos && onIrAAnalisis && (
            <Button type="primary" icon={<RobotOutlined />} onClick={onIrAAnalisis}>
              Pasar al Análisis IA
            </Button>
          )}

          {tieneDocumentos ? (
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
          ) : esCompletadoSinDocs ? (
            <Button
              icon={<ReloadOutlined />}
              loading={enProgreso}
              onClick={() => iniciarDescarga(true)}
            >
              Re-verificar en portal
            </Button>
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
        /* Tarjeta de progreso dinámico en vivo con Stepper */
        <Card
          style={{
            marginBottom: 20,
            background: 'linear-gradient(135deg, rgba(230, 247, 255, 0.95) 0%, rgba(240, 245, 255, 0.95) 100%)',
            borderColor: '#91caff',
            borderRadius: 12,
            boxShadow: '0 4px 12px rgba(22, 119, 255, 0.08)',
          }}
        >
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <Space>
                <SyncOutlined spin style={{ fontSize: 20, color: '#1677ff' }} />
                <Typography.Text strong style={{ fontSize: 16 }}>
                  Extracción en curso desde Mercado Público
                </Typography.Text>
              </Space>
              <Tag color="processing" style={{ fontSize: 13, padding: '3px 10px', borderRadius: 20 }}>
                ⏱️ Tiempo transcurrido: {segundosTranscurridos}s
              </Tag>
            </div>

            <Progress
              percent={porcentajeProgreso}
              status="active"
              strokeColor={{ '0%': '#108ee9', '100%': '#87d068' }}
            />

            {/* Stepper dinámico con las fases del scraper */}
            <div style={{ padding: '8px 0', marginTop: 4 }}>
              <Steps
                current={pasoActual}
                size="small"
                items={[
                  {
                    title: 'Conexión',
                    description: 'Agente navegador',
                    icon: pasoActual === 0 ? <SyncOutlined spin /> : pasoActual > 0 ? <CheckCircleOutlined style={{ color: '#52c41a' }} /> : <GlobalOutlined />,
                  },
                  {
                    title: 'Ficha Oficial',
                    description: 'Acceso portal',
                    icon: pasoActual === 1 ? <SyncOutlined spin /> : pasoActual > 1 ? <CheckCircleOutlined style={{ color: '#52c41a' }} /> : <SafetyCertificateOutlined />,
                  },
                  {
                    title: 'Extracción',
                    description: 'Pliegos y actas',
                    icon: pasoActual === 2 ? <SyncOutlined spin /> : pasoActual > 2 ? <CheckCircleOutlined style={{ color: '#52c41a' }} /> : <CloudDownloadOutlined />,
                  },
                  {
                    title: 'Almacenamiento',
                    description: 'Hash SHA-256 y guardado',
                    icon: pasoActual === 3 ? <SyncOutlined spin /> : <InboxOutlined />,
                  },
                ]}
              />
            </div>

            <div style={{ background: 'rgba(255, 255, 255, 0.75)', padding: '10px 14px', borderRadius: 8 }}>
              <Typography.Text style={{ fontSize: 13, color: '#1f1f1f' }}>
                <strong>Estado actual:</strong> {descripcionesPaso[pasoActual]}
              </Typography.Text>
            </div>
          </Space>
        </Card>
      ) : estado?.estadoConjunto === 'error' ? (
        <Alert
          type="error"
          showIcon
          message="La extracción de documentos no pudo completarse"
          description={estado.descargaError ?? 'Ocurrió un problema al extraer los documentos desde el portal.'}
          action={
            <Button size="small" type="primary" danger icon={<ReloadOutlined />} onClick={() => iniciarDescarga(true)}>
              Reintentar extracción
            </Button>
          }
          style={{ marginBottom: 16, borderRadius: 8 }}
        />
      ) : esCompletadoSinDocs ? (
        /* Estado completado pero el portal no tenía documentos adjuntos */
        <Card
          style={{
            marginBottom: 16,
            borderRadius: 10,
            borderStyle: 'dashed',
            borderColor: '#d9d9d9',
            backgroundColor: '#fafafa',
            textAlign: 'center',
            padding: '20px 10px',
          }}
        >
          <Space direction="vertical" size={8} orientation="center">
            <InfoCircleOutlined style={{ fontSize: 32, color: '#faad14' }} />
            <Typography.Title level={5} style={{ margin: '4px 0' }}>
              Extracción finalizada: Sin documentos adjuntos
            </Typography.Title>
            <Typography.Text type="secondary" style={{ maxWidth: 520, display: 'inline-block' }}>
              El portal oficial de Mercado Público no registra archivos adjuntos ni bases descargables para esta licitación (habitual en compras ágiles o contrataciones menores).
            </Typography.Text>
            <div style={{ marginTop: 12 }}>
              <Button icon={<ReloadOutlined />} onClick={() => iniciarDescarga(true)}>
                Volver a consultar en el portal
              </Button>
            </div>
          </Space>
        </Card>
      ) : !tieneDocumentos ? (
        /* Estado pendiente (nunca se ha solicitado) */
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
          style={{ borderRadius: 8 }}
        />
      ) : (
        /* Estado completado con documentos */
        <>
          <Alert
            type="success"
            showIcon
            icon={<CheckCircleOutlined />}
            message={`Todos los documentos están disponibles (${estado.documentos.length} archivos)`}
            description="Las bases y anexos han sido verificados y almacenados con hash SHA-256. Ya puedes proceder con el análisis inteligente."
            style={{ marginBottom: 16, borderRadius: 8 }}
          />

          <List
            bordered
            size="small"
            style={{ borderRadius: 8, overflow: 'hidden' }}
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
