import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  App as AntdApp,
  Button,
  Card,
  Checkbox,
  Divider,
  Empty,
  Select,
  Space,
  Spin,
  Table,
  Tag,
  Typography,
  theme,
} from 'antd';
import {
  CheckCircleOutlined,
  CheckOutlined,
  CloudUploadOutlined,
  DownloadOutlined,
  ExclamationCircleOutlined,
  FileTextOutlined,
  FileWordOutlined,
  MailOutlined,
  ReloadOutlined,
  RobotOutlined,
  SyncOutlined,
} from '@ant-design/icons';
import { useDecision } from '../hooks/useCenso';
import { useMatchCapacidades } from '../hooks/useCenso';
import {
  descargarPropuesta,
  useActualizarEstadoPropuesta,
  useAvisarDecision,
  useCatalogoCapitulos,
  useCatalogoCertificaciones,
  useCatalogoExperiencias,
  useExportarPropuestaDrive,
  useGenerarPropuesta,
  usePropuestasHistorial,
  useRecomendaciones,
  useSincronizarCertificacionesCensus,
} from '../hooks/usePropuestas';
import type { DecisionEstado } from '../types/licitacion';
import type { PropuestaHistorial, RecomendacionResponse } from '../types/propuestas';

interface Props {
  codigoExterno: string | null;
  onIrADecision?: () => void;
}

function formatFecha(iso: string | null): string {
  if (!iso) return '-';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return new Intl.DateTimeFormat('es-CL', {
    day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
  }).format(date);
}

function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

export function PropuestaPanel({ codigoExterno, onIrADecision }: Props) {
  const { message } = AntdApp.useApp();
  const { token } = theme.useToken();
  const { data: decisionData, isLoading: decisionLoading } = useDecision(codigoExterno);
  const { data: matchData } = useMatchCapacidades(codigoExterno);
  const decision: DecisionEstado | null = decisionData?.data ?? null;
  const match = matchData?.data?.match;

  const proposalEnabled = decision?.decision === 'go';
  const chaptersQuery = useCatalogoCapitulos(proposalEnabled);
  const certificationsQuery = useCatalogoCertificaciones(proposalEnabled);
  const experiencesQuery = useCatalogoExperiencias(proposalEnabled);
  const historyQuery = usePropuestasHistorial(codigoExterno, proposalEnabled);
  const syncCensusMutation = useSincronizarCertificacionesCensus();
  const recommendations = useRecomendaciones();
  const generate = useGenerarPropuesta();
  const updateState = useActualizarEstadoPropuesta();
  const notify = useAvisarDecision();
  const exportDrive = useExportarPropuestaDrive();

  const chapters = chaptersQuery.data?.data?.items ?? [];
  const certifications = certificationsQuery.data?.data?.items ?? [];
  const experiences = experiencesQuery.data?.data?.items ?? [];
  const history = historyQuery.data?.data?.items ?? [];
  const [selectedChapterIds, setSelectedChapterIds] = useState<number[]>([]);
  const [selectedCertificationIds, setSelectedCertificationIds] = useState<number[]>([]);
  const [selectedExperienceIds, setSelectedExperienceIds] = useState<number[]>([]);
  const [rec, setRec] = useState<RecomendacionResponse | null>(null);
  const [destinatarios, setDestinatarios] = useState<string[]>([]);
  const [chaptersInitialized, setChaptersInitialized] = useState(false);

  useEffect(() => {
    if (!chaptersInitialized && chapters.length > 0) {
      setSelectedChapterIds(chapters.map((chapter) => chapter.id));
      setChaptersInitialized(true);
    }
  }, [chapters, chaptersInitialized]);

  useEffect(() => {
    setDestinatarios(decision?.notificados ?? []);
  }, [decision?.decisionId]);

  const matchOptions = useMemo(
    () => (match?.personas ?? []).map((persona) => ({
      value: persona.email,
      label: `${persona.nombre} · ${persona.email}`,
    })),
    [match],
  );

  if (decisionLoading) {
    return (
      <div style={{ textAlign: 'center', padding: 40 }}>
        <Spin tip="Cargando estado de la propuesta comercial..." />
      </div>
    );
  }

  // Si la decisión aún no ha sido registrada formalmente
  if (!decision?.decidida || !decision.decision) {
    return (
      <Card
        style={{
          textAlign: 'center',
          padding: '36px 16px',
          background: token.colorFillAlter,
          borderColor: token.colorBorderSecondary,
          marginTop: 12,
        }}
      >
        <FileWordOutlined style={{ fontSize: 52, color: token.colorPrimary, marginBottom: 16 }} />
        <Typography.Title level={4} style={{ marginBottom: 8 }}>
          Propuesta Comercial & Exportación a Google Drive
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ maxWidth: 640, margin: '0 auto 24px', fontSize: 14 }}>
          Para armar la propuesta técnica/económica editable en formato DOCX, seleccionar certificaciones, experiencias
          y exportar la carpeta a Google Drive, primero se debe registrar la <strong>Decisión Humana (GO / NO GO)</strong>.
        </Typography.Paragraph>
        {onIrADecision && (
          <Button type="primary" size="large" icon={<CheckCircleOutlined />} onClick={onIrADecision}>
            Ir a 4. Decisión GO/NO GO
          </Button>
        )}
      </Card>
    );
  }

  const catalogoLoading = chaptersQuery.isLoading || certificationsQuery.isLoading || experiencesQuery.isLoading;
  const catalogoError = chaptersQuery.error || certificationsQuery.error || experiencesQuery.error;
  const recommendedCertificationIds = rec?.certificaciones
    .filter((item) => item.categoria === 'recomendado')
    .map((item) => item.id) ?? [];
  const recommendedExperienceIds = rec?.experiencias
    .filter((item) => item.categoria === 'recomendado')
    .map((item) => item.id) ?? [];

  const pedirRecomendaciones = () => {
    if (!codigoExterno) return;
    recommendations.mutate({ codigoExterno }, {
      onSuccess: (result) => {
        setRec(result.data);
        message.success('Recomendaciones calculadas; la selección final sigue siendo humana');
      },
      onError: (error) => message.error(error instanceof Error ? error.message : 'No se pudieron obtener recomendaciones'),
    });
  };

  const seleccionarRecomendados = () => {
    setSelectedCertificationIds(recommendedCertificationIds);
    setSelectedExperienceIds(recommendedExperienceIds);
  };

  const generarPropuesta = () => {
    if (!codigoExterno || selectedChapterIds.length === 0) return;
    generate.mutate({
      codigoExterno,
      request: {
        capitulosIds: selectedChapterIds,
        certificacionesIds: selectedCertificationIds,
        experienciasIds: selectedExperienceIds,
      },
    }, {
      onSuccess: (result) => message.success(`Propuesta v${result.data.version} generada exitosamente`),
      onError: (error) => message.error(error instanceof Error ? error.message : 'No se pudo generar la propuesta'),
    });
  };

  const avisar = () => {
    if (!codigoExterno || !decision.decisionId || destinatarios.length === 0) return;
    notify.mutate({ codigoExterno, decisionId: decision.decisionId, destinatarios }, {
      onSuccess: (result) => message.success(`Aviso enviado a ${result.data.enviados} destinatarios`),
      onError: (error) => message.error(error instanceof Error ? error.message : 'No se pudo completar el lote de avisos'),
    });
  };

  const descargar = async (item: PropuestaHistorial) => {
    if (!codigoExterno) return;
    try {
      const blob = await descargarPropuesta(codigoExterno, item.propuestaId);
      downloadBlob(blob, `Propuesta_${codigoExterno}_v${item.version}.docx`);
    } catch (error) {
      message.error(error instanceof Error ? error.message : 'No se pudo descargar el DOCX');
    }
  };

  const cambiarEstado = (item: PropuestaHistorial, estado: 'enviada' | 'descartada') => {
    if (!codigoExterno) return;
    updateState.mutate({ codigoExterno, propuestaId: item.propuestaId, estado }, {
      onSuccess: () => message.success(`Propuesta v${item.version}: estado ${estado}`),
      onError: (error) => message.error(error instanceof Error ? error.message : 'No se pudo cambiar el estado'),
    });
  };

  const exportarDrive = (item: PropuestaHistorial) => {
    if (!codigoExterno) return;
    exportDrive.mutate({ codigoExterno, propuestaId: item.propuestaId }, {
      onSuccess: (result) => message.success(`Propuesta v${item.version} exportada a Google Drive (${result.data.nombreArchivo})`),
      onError: (error) => message.error(error instanceof Error ? error.message : 'No se pudo exportar a Drive'),
    });
  };

  return (
    <div style={{ padding: '8px 0' }}>
      {/* Banner de Decisión Vigente */}
      {decision.decision === 'no_go' ? (
        <Alert
          type="warning"
          showIcon
          icon={<ExclamationCircleOutlined />}
          style={{ marginBottom: 20 }}
          message="Decisión registrada: NO OFERTAR (NO GO)"
          description="La decisión registrada para esta licitación es NO OFERTAR. La generación de propuesta comercial está inhabilitada. Puedes notificar al equipo responsable sobre esta decisión a continuación."
        />
      ) : (
        <Alert
          type="success"
          showIcon
          icon={<CheckCircleOutlined />}
          style={{ marginBottom: 20 }}
          message="Decisión registrada: OFERTAR (GO)"
          description="La decisión para esta licitación es GO. Puedes configurar y generar el borrador de propuesta comercial (.docx), exportarlo a Google Drive y notificar a los participantes."
        />
      )}

      {/* Sección Avisos a Participantes */}
      <Card size="small" style={{ marginBottom: 20 }}>
        <Typography.Title level={5} style={{ margin: '0 0 4px 0' }}>
          Avisos de decisión {decision.decision === 'go' ? 'GO' : 'NO GO'}
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 12, fontSize: 13 }}>
          La decisión es humana. Selecciona colaboradores visibles en el match o agrega correos institucionales para notificarles.
        </Typography.Paragraph>
        <Select
          mode="tags"
          value={destinatarios}
          options={matchOptions}
          tokenSeparators={[',', ';']}
          onChange={setDestinatarios}
          placeholder="Seleccionar personas o escribir correos electrónicos"
          style={{ width: '100%' }}
          maxTagCount="responsive"
          disabled={!decision.decisionId || notify.isPending}
          data-testid="select-destinatarios-decision"
        />
        {decision.notificados && decision.notificados.length > 0 && (
          <Alert
            type="success"
            showIcon
            icon={<CheckOutlined />}
            style={{ marginTop: 12 }}
            message={`Ya notificados (${decision.notificados.length}) · ${formatFecha(decision.notificadoAt)}`}
            description={<Space wrap>{decision.notificados.map((email) => <Tag key={email}>{email}</Tag>)}</Space>}
          />
        )}
        <Button
          type="primary"
          icon={<MailOutlined />}
          loading={notify.isPending}
          disabled={!decision.decisionId || destinatarios.length === 0}
          onClick={avisar}
          style={{ marginTop: 12 }}
          data-testid="btn-avisar-decision"
        >
          Avisar a seleccionados
        </Button>
      </Card>

      {decision.decision === 'go' && (
        <>
          <Typography.Title level={5} style={{ marginTop: 8 }}>
            Generador de Propuesta Comercial (DOCX)
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ fontSize: 13, marginBottom: 16 }}>
            Selecciona los capítulos, certificaciones y casos de éxito de TIVIT que se integrarán en el documento final.
          </Typography.Paragraph>

          {catalogoLoading ? <Spin /> : catalogoError ? (
            <Alert
              type="error"
              showIcon
              message="No se pudieron cargar los catálogos"
              description="Verifica que los catálogos corporativos estén disponibles y vuelve a abrir la ficha."
              action={<Button size="small" icon={<ReloadOutlined />} onClick={() => { void chaptersQuery.refetch(); void certificationsQuery.refetch(); void experiencesQuery.refetch(); }}>Reintentar</Button>}
            />
          ) : (
            <Card size="small" style={{ marginBottom: 20 }}>
              <Space wrap style={{ marginBottom: 16 }}>
                <Button icon={<RobotOutlined />} loading={recommendations.isPending} onClick={pedirRecomendaciones} data-testid="btn-obtener-recomendaciones">
                  Obtener recomendaciones con IA
                </Button>
                {rec && (
                  <Button onClick={seleccionarRecomendados} disabled={recommendedCertificationIds.length + recommendedExperienceIds.length === 0}>
                    Seleccionar recomendados ({recommendedCertificationIds.length + recommendedExperienceIds.length})
                  </Button>
                )}
              </Space>
              {rec && (
                <Alert
                  type="info"
                  showIcon
                  style={{ marginBottom: 16 }}
                  message={`Recomendaciones IA: ${rec.resumen.recomendados} recomendadas, ${rec.resumen.posibles} posibles`}
                  description="Las recomendaciones son orientativas y no reemplazan la decisión del líder de propuesta."
                />
              )}

              <div style={{ marginBottom: 16 }}>
                <Typography.Text strong style={{ display: 'block', marginBottom: 6 }}>
                  Capítulos del Documento ({selectedChapterIds.length}/{chapters.length})
                </Typography.Text>
                <Checkbox.Group
                  value={selectedChapterIds}
                  onChange={(values) => setSelectedChapterIds(values.map(Number))}
                  options={chapters.map((chapter) => ({ label: `${chapter.orden}. ${chapter.titulo}`, value: chapter.id }))}
                  style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}
                />
              </div>

              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
                  <Typography.Text strong>
                    Certificaciones Corporativas TIVIT ({certifications.length})
                  </Typography.Text>
                  <Space size={8}>
                    <Button
                      type="link"
                      size="small"
                      icon={<SyncOutlined spin={syncCensusMutation.isPending} />}
                      loading={syncCensusMutation.isPending}
                      onClick={() =>
                        syncCensusMutation.mutate(undefined, {
                          onSuccess: (res) => {
                            message.success(`Sincronización Census completada: ${res.data?.insertadas ?? 0} nuevas`);
                            void certificationsQuery.refetch();
                          },
                        })
                      }
                    >
                      Sincronizar Census
                    </Button>
                    <a href="/catalogos?tab=certificaciones" target="_blank" rel="noopener noreferrer" style={{ fontSize: 12 }}>
                      Ver Catálogo ↗
                    </a>
                  </Space>
                </div>
                <Select
                  mode="multiple"
                  value={selectedCertificationIds}
                  onChange={setSelectedCertificationIds}
                  options={certifications.map((certification) => ({
                    label: `${certification.nombre}${certification.fileIdCensus ? ' 📄' : ''}`,
                    value: certification.id,
                  }))}
                  placeholder="Seleccionar certificaciones a incluir"
                  style={{ width: '100%' }}
                  data-testid="select-certificaciones-propuesta"
                  maxTagCount="responsive"
                />
                {certifications.some((certification) => !certification.fileIdCensus) && (
                  <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginTop: 4 }}>
                    Nota: Las certificaciones sin PDF asociado se incorporarán como texto en los anexos.
                  </Typography.Text>
                )}
              </div>

              <div style={{ marginBottom: 20 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
                  <Typography.Text strong>
                    Experiencias y Casos de Éxito ({experiences.length})
                  </Typography.Text>
                  <a href="/catalogos?tab=experiencias" target="_blank" rel="noopener noreferrer" style={{ fontSize: 12 }}>
                    + Nuevo Caso / Catálogo ↗
                  </a>
                </div>
                <Select
                  mode="multiple"
                  value={selectedExperienceIds}
                  onChange={setSelectedExperienceIds}
                  options={experiences.map((experience) => ({
                    label: `${experience.titulo} · ${experience.cliente}`,
                    value: experience.id,
                  }))}
                  placeholder="Seleccionar experiencias relevantes"
                  style={{ width: '100%' }}
                  data-testid="select-experiencias-propuesta"
                  maxTagCount="responsive"
                />
              </div>

              <Button
                type="primary"
                size="large"
                icon={<FileTextOutlined />}
                loading={generate.isPending}
                disabled={selectedChapterIds.length === 0}
                onClick={generarPropuesta}
                data-testid="btn-generar-propuesta"
              >
                Generar Propuesta DOCX
              </Button>
            </Card>
          )}

          <Divider />

          <Typography.Title level={5}>
            Historial de Versiones y Google Drive
          </Typography.Title>
          {historyQuery.isLoading ? <Spin /> : history.length === 0 ? <Empty description="Aún no hay propuestas generadas para esta licitación" /> : (
            <Table<PropuestaHistorial>
              size="small"
              rowKey="propuestaId"
              pagination={false}
              dataSource={history}
              columns={[
                { title: 'Versión', dataIndex: 'version', width: 80, render: (version: number) => <Tag color="blue">v{version}</Tag> },
                {
                  title: 'Estado',
                  dataIndex: 'estado',
                  width: 110,
                  render: (estado: string) => {
                    const color = estado === 'enviada' ? 'success' : estado === 'descartada' ? 'default' : 'processing';
                    return <Tag color={color}>{estado.toUpperCase()}</Tag>;
                  },
                },
                { title: 'Contenido', render: (_, item) => `${item.capitulos} cap. · ${item.certificaciones} cert. · ${item.experiencias} exp.` },
                { title: 'Generada', dataIndex: 'generadoAt', render: (value: string | null) => formatFecha(value) },
                {
                  title: 'Acciones',
                  render: (_, item) => (
                    <Space size={6} wrap>
                      {item.rutaDescarga && (
                        <Button type="primary" ghost size="small" icon={<DownloadOutlined />} onClick={() => void descargar(item)} data-testid={`btn-descargar-propuesta-${item.propuestaId}`}>
                          Descargar DOCX
                        </Button>
                      )}
                      {item.rutaDescarga && (
                        <Button size="small" icon={<CloudUploadOutlined />} loading={exportDrive.isPending} onClick={() => exportarDrive(item)} data-testid={`btn-exportar-drive-${item.propuestaId}`}>
                          Exportar a Drive
                        </Button>
                      )}
                      {item.estado === 'generada' && (
                        <Button size="small" onClick={() => cambiarEstado(item, 'enviada')}>
                          Marcar enviada
                        </Button>
                      )}
                      {(item.estado === 'generada' || item.estado === 'enviada') && (
                        <Button size="small" danger onClick={() => cambiarEstado(item, 'descartada')}>
                          Descartar
                        </Button>
                      )}
                    </Space>
                  ),
                },
              ]}
            />
          )}
        </>
      )}
    </div>
  );
}
