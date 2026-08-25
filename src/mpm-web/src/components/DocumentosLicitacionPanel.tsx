import { useState, useRef } from 'react';
import {
  Alert,
  Button,
  Card,
  List,
  Space,
  Spin,
  Tag,
  Typography,
  Upload,
  App as AntdApp,
} from 'antd';
import {
  DownloadOutlined,
  FilePdfOutlined,
  FileWordOutlined,
  FileExcelOutlined,
  FileZipOutlined,
  FileTextOutlined,
  CheckCircleOutlined,
  RobotOutlined,
  LinkOutlined,
  CloudUploadOutlined,
  InboxOutlined,
  DeleteOutlined,
} from '@ant-design/icons';
import {
  descargarArchivoDocumento,
  formatTamanio,
  useEstadoDocumentos,
  useUploadManualDocumentos,
} from '../hooks/useDocumentosLicitacion';
import type { LicitacionDocumentoItem } from '../types/licitacion';

const { Dragger } = Upload;

interface Props {
  codigoExterno: string;
  onIrAAnalisis?: () => void;
}

function getFileIcon(nombre: string) {
  const ext = nombre.split('.').pop()?.toLowerCase();
  if (ext === 'pdf') return <FilePdfOutlined style={{ fontSize: 22, color: '#cf1322' }} />;
  if (ext === 'doc' || ext === 'docx') return <FileWordOutlined style={{ fontSize: 22, color: '#0958d9' }} />;
  if (ext === 'xls' || ext === 'xlsx') return <FileExcelOutlined style={{ fontSize: 22, color: '#389e0d' }} />;
  if (ext === 'zip' || ext === 'rar') return <FileZipOutlined style={{ fontSize: 22, color: '#d48806' }} />;
  return <FileTextOutlined style={{ fontSize: 22, color: '#595959' }} />;
}

function fichaUrl(codigo: string) {
  return `https://www.mercadopublico.cl/fichaLicitacion.html?idlicitacion=${encodeURIComponent(codigo)}`;
}

export function DocumentosLicitacionPanel({ codigoExterno, onIrAAnalisis }: Props) {
  const { message } = AntdApp.useApp();
  const { data: estadoData, isLoading: estadoLoading } = useEstadoDocumentos(codigoExterno);
  const uploadMutation = useUploadManualDocumentos();
  const estado = estadoData?.data;

  const [fileList, setFileList] = useState<File[]>([]);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const tieneDocumentos = estado?.estadoConjunto === 'completado' && (estado.documentos?.length ?? 0) > 0;
  const esCompletadoSinDocs = estado?.estadoConjunto === 'completado' && (estado.documentos?.length ?? 0) === 0;

  const handleUpload = () => {
    if (fileList.length === 0) {
      message.warning('Selecciona al menos un archivo');
      return;
    }
    uploadMutation.mutate(
      { codigoExterno, files: fileList },
      {
        onSuccess: (res: any) => {
          const d = res?.data;
          if (d?.rechazados > 0) {
            message.warning(d.mensaje || `Carga parcial: ${d.descargados + d.reutilizados} ok, ${d.rechazados} rechazados`);
          } else {
            message.success(d?.mensaje || `Se cargaron ${d?.descargados ?? fileList.length} archivos correctamente`);
          }
          setFileList([]);
        },
        onError: (e: any) => {
          const msg = e?.message || 'No se pudo subir los archivos';
          // DOC_007 = auto deshabilitado, pero este es manual, no aplica
          message.error(msg);
        },
      },
    );
  };

  const descargarLocal = (doc: LicitacionDocumentoItem) => {
    descargarArchivoDocumento(codigoExterno, doc).catch(() => message.error('No se pudo descargar el archivo'));
  };

  const beforeUpload = (file: File) => {
    const ext = file.name.split('.').pop()?.toLowerCase() ?? '';
    const permitidas = ['pdf', 'doc', 'docx', 'xls', 'xlsx', 'zip', 'rar', 'txt'];
    if (!permitidas.includes(ext)) {
      message.error(`${file.name}: extensión .${ext} no permitida`);
      return Upload.LIST_IGNORE;
    }
    if (file.size > 20 * 1024 * 1024) {
      message.error(`${file.name}: supera 20MB`);
      return Upload.LIST_IGNORE;
    }
    if (fileList.length >= 10) {
      message.warning('Máximo 10 archivos por carga');
      return Upload.LIST_IGNORE;
    }
    // No auto upload, manejamos manualmente
    setFileList((prev) => [...prev, file]);
    return false;
  };

  return (
    <div style={{ padding: '8px 0' }}>
      {/* Header */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'flex-start',
          marginBottom: 20,
          flexWrap: 'wrap',
          gap: 12,
        }}
      >
        <div>
          <Typography.Title level={5} style={{ margin: 0, fontWeight: 700, letterSpacing: -0.2 }}>
            Pliegos y Bases Oficiales
            {tieneDocumentos && (
              <Tag color="success" style={{ marginLeft: 8, borderRadius: 4 }}>
                {estado!.documentos.length} archivos
              </Tag>
            )}
            {esCompletadoSinDocs && (
              <Tag style={{ marginLeft: 8, borderRadius: 4 }}>Sin archivos</Tag>
            )}
          </Typography.Title>
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            Carga manual — descarga desde Mercado Público y sube aquí para analizar con IA.
          </Typography.Text>
        </div>

        <Space size={8} wrap>
          <Button
            icon={<LinkOutlined />}
            href={fichaUrl(codigoExterno)}
            target="_blank"
            rel="noopener noreferrer"
          >
            Ver en Mercado Público
          </Button>
          {tieneDocumentos && onIrAAnalisis && (
            <Button type="primary" icon={<RobotOutlined />} onClick={onIrAAnalisis}>
              Analizar con IA
            </Button>
          )}
        </Space>
      </div>

      {/* Info permanente: flujo manual */}
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16, borderRadius: 8, border: '1px solid #d6e4ff', background: '#f0f5ff' }}
        message="Flujo manual activo"
        description={
          <span>
            Mercado Público protege los adjuntos con reCAPTCHA Enterprise.{' '}
            <Typography.Text strong style={{ color: '#1d39c4' }}>
              Abre la ficha oficial, descarga los pliegos a tu PC y arrástralos a la zona de carga.
            </Typography.Text>{' '}
            El análisis IA (Go/No-Go) se habilita automáticamente al subir.
          </span>
        }
      />

      {/* Dropzone */}
      <Card
        size="small"
        style={{
          marginBottom: 16,
          borderRadius: 10,
          borderStyle: 'dashed',
          background: '#fafafa',
        }}
        bodyStyle={{ padding: 16 }}
      >
        <Dragger
          multiple
          maxCount={10}
          fileList={[]}
          beforeUpload={beforeUpload}
          showUploadList={false}
          accept=".pdf,.doc,.docx,.xls,.xlsx,.zip,.rar,.txt"
          disabled={uploadMutation.isPending}
          style={{ background: '#fff', borderRadius: 8 }}
        >
          <p className="ant-upload-drag-icon">
            <InboxOutlined style={{ color: '#1677ff', fontSize: 32 }} />
          </p>
          <p className="ant-upload-text" style={{ fontWeight: 600 }}>
            Arrastra los pliegos aquí o haz click para seleccionar
          </p>
          <p className="ant-upload-hint" style={{ color: '#8c8c8c' }}>
            PDF, DOC, DOCX, XLS, XLSX, ZIP, TXT — hasta 20MB por archivo, máx. 10 archivos
          </p>
        </Dragger>

        {/* Lista de archivos por subir (seleccionados) */}
        {fileList.length > 0 && (
          <div style={{ marginTop: 16 }}>
            <Typography.Text strong style={{ fontSize: 13 }}>
              Archivos seleccionados ({fileList.length}):
            </Typography.Text>
            <List
              size="small"
              style={{ marginTop: 8, background: '#fff', borderRadius: 8 }}
              bordered
              dataSource={fileList}
              renderItem={(file, idx) => (
                <List.Item
                  actions={[
                    <Button
                      key="remove"
                      type="text"
                      size="small"
                      danger
                      icon={<DeleteOutlined />}
                      onClick={() => setFileList((prev) => prev.filter((_, i) => i !== idx))}
                    />,
                  ]}
                >
                  <List.Item.Meta
                    avatar={getFileIcon(file.name)}
                    title={<span style={{ fontSize: 13 }}>{file.name}</span>}
                    description={`${formatTamanio(file.size)}`}
                  />
                </List.Item>
              )}
            />
            <div style={{ marginTop: 12, display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
              <Button onClick={() => setFileList([])} disabled={uploadMutation.isPending}>
                Limpiar
              </Button>
              <Button
                type="primary"
                icon={<CloudUploadOutlined />}
                loading={uploadMutation.isPending}
                onClick={handleUpload}
              >
                Subir {fileList.length} archivo{fileList.length > 1 ? 's' : ''}
              </Button>
            </div>
          </div>
        )}
      </Card>

      {/* Input oculto fallback */}
      <input
        ref={fileInputRef}
        type="file"
        multiple
        accept=".pdf,.doc,.docx,.xls,.xlsx,.zip,.rar,.txt"
        style={{ display: 'none' }}
        onChange={(e) => {
          const files = Array.from(e.target.files ?? []);
          files.forEach((f) => beforeUpload(f));
          if (fileInputRef.current) fileInputRef.current.value = '';
        }}
      />

      {/* Estados de documentos ya persistidos */}
      {estadoLoading && !estado ? (
        <div style={{ textAlign: 'center', padding: 24 }}>
          <Spin tip="Consultando documentos..." />
        </div>
      ) : tieneDocumentos ? (
        <>
          <Alert
            type="success"
            showIcon
            icon={<CheckCircleOutlined />}
            message={`Documentos listos para análisis (${estado!.documentos.length})`}
            description="Verificados con SHA-256 y almacenados. Ya puedes ejecutar el análisis comercial."
            style={{ marginBottom: 12, borderRadius: 8 }}
          />
          <List
            bordered
            size="small"
            style={{ borderRadius: 8, overflow: 'hidden', background: '#fff' }}
            dataSource={estado!.documentos}
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
                  title={
                    <Typography.Text strong style={{ fontSize: 13 }}>
                      {doc.nombreArchivo}
                    </Typography.Text>
                  }
                  description={
                    <Space size={12} wrap>
                      <Tag
                        style={{
                          borderRadius: 4,
                          fontSize: 11,
                          textTransform: 'uppercase',
                          letterSpacing: 0.3,
                        }}
                        color={doc.esActa ? 'purple' : 'blue'}
                      >
                        {doc.esActa ? 'Acta' : 'Anexo'}
                      </Tag>
                      <span style={{ color: '#595959', fontSize: 12 }}>Tamaño: {formatTamanio(doc.tamanioBytes)}</span>
                      <span style={{ color: '#595959', fontSize: 12 }}>v{doc.version}</span>
                      {doc.fechaGrilla && <span style={{ color: '#8c8c8c', fontSize: 12 }}>{doc.fechaGrilla}</span>}
                    </Space>
                  }
                />
              </List.Item>
            )}
          />
          {estado!.conjuntoHash && (
            <div style={{ marginTop: 10 }}>
              <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                Hash conjunto: <code style={{ background: '#f5f5f5', padding: '2px 6px', borderRadius: 4 }}>{estado!.conjuntoHash.slice(0, 24)}…</code>
              </Typography.Text>
            </div>
          )}
        </>
      ) : esCompletadoSinDocs ? (
        <Alert
          type="warning"
          showIcon
          message="Aún no hay documentos cargados"
          description="Sube los pliegos descargados desde Mercado Público para habilitar el análisis."
          style={{ borderRadius: 8 }}
        />
      ) : estado?.estadoConjunto === 'error' ? (
        <Alert
          type="error"
          showIcon
          message="Error en la carga previa"
          description={estado.descargaError ?? 'Ocurrió un problema al procesar los documentos.'}
          style={{ borderRadius: 8 }}
        />
      ) : (
        <Alert
          type="warning"
          showIcon
          message="Sin documentos"
          description="Aún no has subido pliegos para esta licitación. Usa la zona de carga superior."
          style={{ borderRadius: 8 }}
        />
      )}

      <div style={{ marginTop: 16, padding: '10px 12px', background: '#fffbe6', border: '1px solid #ffe58f', borderRadius: 8 }}>
        <Typography.Text style={{ fontSize: 12, color: '#595959' }}>
          <strong>Nota de referencia:</strong> la descarga automatizada está deprecada por bloqueo reCAPTCHA Enterprise (ADR-015).
          Se conserva en <code>tools/scraper-mp-v2/descargar-documentos.js</code> como referencia. Modo actual: <code>manual</code>.
          Otros scrapers (licitaciones, ficha, competidores) siguen operativos.
        </Typography.Text>
      </div>
    </div>
  );
}
