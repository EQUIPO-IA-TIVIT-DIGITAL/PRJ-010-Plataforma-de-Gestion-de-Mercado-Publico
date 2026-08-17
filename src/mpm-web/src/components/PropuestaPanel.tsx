import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  App as AntdApp,
  Button,
  Checkbox,
  Divider,
  Empty,
  Select,
  Space,
  Spin,
  Table,
  Tag,
  Typography,
} from 'antd';
import {
  CheckOutlined,
  DownloadOutlined,
  FileTextOutlined,
  MailOutlined,
  ReloadOutlined,
  RobotOutlined,
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
  useGenerarPropuesta,
  usePropuestasHistorial,
  useRecomendaciones,
} from '../hooks/usePropuestas';
import type { DecisionEstado } from '../types/licitacion';
import type { PropuestaHistorial, RecomendacionResponse } from '../types/propuestas';

interface Props {
  codigoExterno: string | null;
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

export function PropuestaPanel({ codigoExterno }: Props) {
  const { message } = AntdApp.useApp();
  const { data: decisionData, isLoading: decisionLoading } = useDecision(codigoExterno);
  const { data: matchData } = useMatchCapacidades(codigoExterno);
  const decision: DecisionEstado | null = decisionData?.data ?? null;
  const match = matchData?.data?.match;

  const proposalEnabled = decision?.decision === 'go';
  const chaptersQuery = useCatalogoCapitulos(proposalEnabled);
  const certificationsQuery = useCatalogoCertificaciones(proposalEnabled);
  const experiencesQuery = useCatalogoExperiencias(proposalEnabled);
  const historyQuery = usePropuestasHistorial(codigoExterno, proposalEnabled);
  const recommendations = useRecomendaciones();
  const generate = useGenerarPropuesta();
  const updateState = useActualizarEstadoPropuesta();
  const notify = useAvisarDecision();

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

  if (decisionLoading || !decision?.decidida || !decision.decision) return null;

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
      onSuccess: (result) => message.success(`Propuesta v${result.data.version} generada`),
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

  return (
    <>
      <Typography.Title level={5} style={{ marginTop: 24 }}>
        Avisos de decisión {decision.decision === 'go' ? 'GO' : 'NO GO'}
      </Typography.Title>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
        La decisión es humana; la recomendación IA no dispara avisos. Selecciona personas visibles en el match o agrega emails válidos.
        El aviso oficial es in-app y nunca se envía como broadcast.
      </Typography.Paragraph>
      <Select
        mode="tags"
        value={destinatarios}
        options={matchOptions}
        tokenSeparators={[',', ';']}
        onChange={setDestinatarios}
        placeholder="Seleccionar personas o escribir emails"
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
          style={{ marginTop: 8 }}
          message={`Ya notificados (${decision.notificados.length}) · ${formatFecha(decision.notificadoAt)}`}
          description={<Space wrap>{decision.notificados.map((email) => <Tag key={email}>{email}</Tag>)}</Space>}
        />
      )}
      {!decision.decisionId && (
        <Alert type="warning" showIcon style={{ marginTop: 8 }} message="La decisión aún no tiene identificador para avisar; actualiza la ficha." />
      )}
      <Button
        type="primary"
        icon={<MailOutlined />}
        loading={notify.isPending}
        disabled={!decision.decisionId || destinatarios.length === 0}
        onClick={avisar}
        style={{ marginTop: 8 }}
        data-testid="btn-avisar-decision"
      >
        Avisar seleccionados
      </Button>

      {decision.decision === 'go' && (
        <>
          <Divider />
          <Typography.Title level={5}>
            Propuesta comercial
          </Typography.Title>
          <Typography.Paragraph type="secondary">
            La decisión humana es GO. Revisa las sugerencias y selecciona el contenido antes de generar cada versión.
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
            <>
              <Space wrap style={{ marginBottom: 12 }}>
                <Button icon={<RobotOutlined />} loading={recommendations.isPending} onClick={pedirRecomendaciones} data-testid="btn-obtener-recomendaciones">
                  Obtener recomendaciones
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
                  style={{ marginBottom: 12 }}
                  message={`Recomendaciones: ${rec.resumen.recomendados} recomendadas, ${rec.resumen.posibles} posibles`}
                  description="Las recomendaciones son sugerencias; no cambian la selección automáticamente."
                />
              )}

              <Typography.Text strong>Capítulos ({selectedChapterIds.length}/{chapters.length})</Typography.Text>
              <Checkbox.Group
                value={selectedChapterIds}
                onChange={(values) => setSelectedChapterIds(values.map(Number))}
                options={chapters.map((chapter) => ({ label: `${chapter.orden}. ${chapter.titulo}`, value: chapter.id }))}
                style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', margin: '8px 0 16px' }}
              />

              <Typography.Text strong>Certificaciones</Typography.Text>
              <Select
                mode="multiple"
                value={selectedCertificationIds}
                onChange={setSelectedCertificationIds}
                options={certifications.map((certification) => ({
                  label: `${certification.nombre}${certification.fileIdCensus ? '' : ' · sin PDF'}`,
                  value: certification.id,
                }))}
                placeholder="Seleccionar certificaciones"
                style={{ width: '100%', margin: '8px 0 16px' }}
                data-testid="select-certificaciones-propuesta"
              />
              {certifications.some((certification) => !certification.fileIdCensus) && (
                <Alert type="warning" showIcon style={{ marginBottom: 12 }} message="Algunas certificaciones no tienen PDF; se incorporarán como texto." />
              )}

              <Typography.Text strong>Experiencias</Typography.Text>
              <Select
                mode="multiple"
                value={selectedExperienceIds}
                onChange={setSelectedExperienceIds}
                options={experiences.map((experience) => ({ label: `${experience.titulo} · ${experience.cliente}`, value: experience.id }))}
                placeholder="Seleccionar experiencias"
                style={{ width: '100%', margin: '8px 0 16px' }}
                data-testid="select-experiencias-propuesta"
              />

              <Button
                type="primary"
                icon={<FileTextOutlined />}
                loading={generate.isPending}
                disabled={selectedChapterIds.length === 0}
                onClick={generarPropuesta}
                data-testid="btn-generar-propuesta"
              >
                Generar propuesta DOCX
              </Button>
            </>
          )}

          <Divider />
          <Typography.Title level={5}>Historial de versiones</Typography.Title>
          {historyQuery.isLoading ? <Spin /> : history.length === 0 ? <Empty description="Aún no hay propuestas generadas" /> : (
            <Table<PropuestaHistorial>
              size="small"
              rowKey="propuestaId"
              pagination={false}
              dataSource={history}
              columns={[
                { title: 'Versión', dataIndex: 'version', width: 70, render: (version: number) => `v${version}` },
                { title: 'Estado', dataIndex: 'estado', width: 100 },
                { title: 'Contenido', render: (_, item) => `${item.capitulos} cap. · ${item.certificaciones} cert. · ${item.experiencias} exp.` },
                { title: 'Generada', dataIndex: 'generadoAt', render: (value: string | null) => formatFecha(value) },
                {
                  title: 'Acciones',
                  render: (_, item) => (
                    <Space size={4} wrap>
                      {item.rutaDescarga && <Button type="link" size="small" icon={<DownloadOutlined />} onClick={() => void descargar(item)} data-testid={`btn-descargar-propuesta-${item.propuestaId}`}>DOCX</Button>}
                      {item.estado === 'generada' && <Button type="link" size="small" onClick={() => cambiarEstado(item, 'enviada')}>Marcar enviada</Button>}
                      {(item.estado === 'generada' || item.estado === 'enviada') && <Button type="link" danger size="small" onClick={() => cambiarEstado(item, 'descartada')}>Descartar</Button>}
                    </Space>
                  ),
                },
              ]}
            />
          )}
        </>
      )}
    </>
  );
}
