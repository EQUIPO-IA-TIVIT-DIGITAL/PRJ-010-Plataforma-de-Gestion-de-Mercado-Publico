import { useState } from 'react';
import {
  Alert,
  App as AntdApp,
  Button,
  Card,
  Divider,
  Input,
  Modal,
  Space,
  Spin,
  Tag,
  Typography,
} from 'antd';
import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  ReloadOutlined,
  RobotOutlined,
  AuditOutlined,
  EditOutlined,
} from '@ant-design/icons';
import { useAnalisisComercialEstado } from '../hooks/useAnalisisComercial';
import { useDecision, useRegistrarDecision } from '../hooks/useCenso';
import type { DecisionEstado } from '../types/licitacion';

const MOTIVO_MIN = 10;

interface Props {
  codigoExterno: string | null;
}

function formatFecha(iso: string | null): string {
  if (!iso) return '-';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return iso;
  return new Intl.DateTimeFormat('es-CL', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(d);
}

function recomendacionTag(go: string | null) {
  if (!go) return null;
  const map: Record<string, { color: string; label: string }> = {
    strong_go: { color: 'green', label: 'OFERTAR (RECOMENDADO)' },
    go: { color: 'green', label: 'OFERTAR (GO)' },
    no_go: { color: 'orange', label: 'NO OFERTAR (NO GO)' },
    strong_no_go: { color: 'error', label: 'NO OFERTAR (CRÍTICO)' },
  };
  const m = map[go] ?? { color: 'default', label: go.toUpperCase() };
  return <Tag color={m.color} style={{ fontWeight: 600 }}>{m.label}</Tag>;
}

export function DecisionGoNoGoPanel({ codigoExterno }: Props) {
  const { message, modal } = AntdApp.useApp();
  const { data, isLoading, error, refetch } = useDecision(codigoExterno);
  const { data: analisisData } = useAnalisisComercialEstado(codigoExterno);
  const registrar = useRegistrarDecision();
  const [redecidiendo, setRedecidiendo] = useState(false);
  const [modalNoGo, setModalNoGo] = useState(false);
  const [motivo, setMotivo] = useState('');

  const decision: DecisionEstado | null = data?.data ?? null;
  const analisis = analisisData?.data;
  const recomendacion = analisis?.estado === 'completado' ? analisis.goNoGo : null;
  const confianza = analisis?.estado === 'completado' ? analisis.scoreConfianza : null;

  const enviarDecision = (valor: 'go' | 'no_go', mot: string | null) => {
    if (!codigoExterno) return;
    registrar.mutate(
      { codigoExterno, decision: valor, motivo: mot ?? undefined },
      {
        onSuccess: () => {
          message.success(
            valor === 'go'
              ? 'Decisión registrada: TIVIT ofertará en esta licitación'
              : 'Decisión registrada: Licitación descartada para oferta',
          );
          setModalNoGo(false);
          setMotivo('');
          setRedecidiendo(false);
        },
        onError: (e) => message.error(e instanceof Error ? e.message : 'No se pudo registrar la decisión'),
      },
    );
  };

  const confirmarGo = () => {
    modal.confirm({
      title: 'Confirmar Decisión: OFERTAR (GO)',
      icon: <CheckCircleOutlined style={{ color: '#52c41a' }} />,
      content:
        'Se registrará formalmente la decisión de participar y ofertar en esta licitación, habilitando la elaboración de la propuesta técnica/económica (.docx) y la exportación a Google Drive.',
      okText: 'Sí, confirmar y ofertar',
      okButtonProps: { type: 'primary' },
      cancelText: 'Cancelar',
      onOk: () => enviarDecision('go', null),
    });
  };

  const abrirModalNoGo = () => {
    setMotivo('');
    setModalNoGo(true);
  };

  const confirmarNoGo = () => {
    const mot = motivo.trim();
    if (mot.length < MOTIVO_MIN) {
      message.warning(`El motivo debe tener al menos ${MOTIVO_MIN} caracteres`);
      return;
    }
    enviarDecision('no_go', mot);
  };

  const renderAcciones = () => (
    <Space size={12} wrap style={{ marginTop: 12 }}>
      <Button
        type="primary"
        size="large"
        icon={<CheckCircleOutlined />}
        loading={registrar.isPending}
        disabled={!codigoExterno}
        onClick={confirmarGo}
        data-testid="btn-decision-go"
        style={{ background: '#389e0d', borderColor: '#389e0d' }}
      >
        OFERTAR (GO)
      </Button>
      <Button
        danger
        size="large"
        icon={<CloseCircleOutlined />}
        loading={registrar.isPending}
        disabled={!codigoExterno}
        onClick={abrirModalNoGo}
        data-testid="btn-decision-no-go"
      >
        NO OFERTAR (NO GO)
      </Button>
      {redecidiendo && (
        <Button size="large" onClick={() => setRedecidiendo(false)}>
          Mantener decisión actual
        </Button>
      )}
    </Space>
  );

  const renderDecisionRegistrada = (d: DecisionEstado) => (
    <Card
      size="small"
      style={{
        borderLeft: `5px solid ${d.decision === 'go' ? '#52c41a' : '#ff4d4f'}`,
        marginBottom: 16,
      }}
      data-testid="badge-decision"
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 12 }}>
        <div>
          <Space align="center" size={8} style={{ marginBottom: 6 }}>
            <Tag
              color={d.decision === 'go' ? 'success' : 'error'}
              style={{ fontSize: 13, padding: '4px 10px', fontWeight: 700 }}
            >
              {d.decision === 'go' ? 'DECISIÓN: OFERTAR (GO)' : 'DECISIÓN: NO OFERTAR (NO GO)'}
            </Tag>
            {d.recomendacionIa && (
              <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                Sugerencia IA al momento de decidir: {recomendacionTag(d.recomendacionIa)}
              </Typography.Text>
            )}
          </Space>

          {d.motivo && (
            <Typography.Paragraph style={{ margin: '8px 0 4px 0', fontSize: 13 }}>
              <Typography.Text strong>Motivo registrado: </Typography.Text>
              {d.motivo}
            </Typography.Paragraph>
          )}

          <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginTop: 4 }}>
            <AuditOutlined style={{ marginRight: 4 }} />
            Registrado por <strong>{d.decididoPor ?? 'Usuario Comercial'}</strong> el {formatFecha(d.decididoAt)}
          </Typography.Text>
        </div>

        <Button
          icon={<EditOutlined />}
          onClick={() => setRedecidiendo(true)}
          data-testid="btn-cambiar-decision"
        >
          Modificar decisión
        </Button>
      </div>
    </Card>
  );

  return (
    <div style={{ padding: '8px 0' }}>
      <div style={{ marginBottom: 16 }}>
        <Typography.Title level={5} style={{ margin: 0 }}>
          4. Decisión Comercial (GO / NO GO)
        </Typography.Title>
        <Typography.Text type="secondary" style={{ fontSize: 13 }}>
          Postura oficial del equipo comercial respecto a la participación de TIVIT en esta licitación.
        </Typography.Text>
      </div>

      {/* Banner Orientativo de la IA */}
      <Card size="small" style={{ marginBottom: 20, background: '#fafafa' }}>
        <Space wrap align="center">
          <Typography.Text strong>
            <RobotOutlined style={{ marginRight: 6, color: '#1677ff' }} />
            Evaluación orientativa de la IA:
          </Typography.Text>
          {recomendacion ? (
            <Space size={8}>
              {recomendacionTag(recomendacion)}
              {confianza != null && (
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  Nivel de confianza: {(confianza * 100).toFixed(0)}%
                </Typography.Text>
              )}
            </Space>
          ) : (
            <Typography.Text type="secondary">
              Aún no se ha ejecutado el análisis de pliegos con IA.
            </Typography.Text>
          )}
        </Space>
      </Card>

      {isLoading && !decision ? (
        <div style={{ textAlign: 'center', padding: 40 }}>
          <Spin tip="Consultando decisión comercial..." />
        </div>
      ) : error && !decision ? (
        <Alert
          type="error"
          showIcon
          message="No se pudo consultar la decisión comercial"
          description={error instanceof Error ? error.message : 'Intente nuevamente'}
          action={
            <Button size="small" icon={<ReloadOutlined />} onClick={() => refetch()}>
              Reintentar
            </Button>
          }
        />
      ) : !decision || !decision.decidida || redecidiendo ? (
        <Card style={{ borderColor: '#d9d9d9' }}>
          <Typography.Title level={5} style={{ marginTop: 0, marginBottom: 8 }}>
            {redecidiendo ? 'Modificar Decisión Comercial' : 'Registro de Decisión Comercial'}
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 16, fontSize: 13 }}>
            {redecidiendo
              ? 'Puedes cambiar la postura de participación. La nueva decisión actualizará el expediente comercial y quedará registrada en la auditoría del proceso.'
              : 'Selecciona formalmente si TIVIT participará y presentará oferta en este proceso licitatorio. El registro almacenará la persona responsable y la fecha exacta.'}
          </Typography.Paragraph>
          {renderAcciones()}
        </Card>
      ) : (
        renderDecisionRegistrada(decision)
      )}

      {/* Modal para justificar NO GO */}
      <Modal
        open={modalNoGo}
        title="Registrar Decisión: NO OFERTAR (NO GO)"
        okText="Confirmar descarte"
        okButtonProps={{ danger: true, disabled: motivo.trim().length < MOTIVO_MIN }}
        cancelText="Cancelar"
        onOk={confirmarNoGo}
        onCancel={() => setModalNoGo(false)}
        data-testid="modal-motivo-no-go"
      >
        <Typography.Paragraph type="secondary" style={{ fontSize: 13, marginTop: 8 }}>
          Indica el motivo técnico, comercial o logístico por el cual TIVIT no participará en este proceso (mínimo {MOTIVO_MIN} caracteres). Quedará guardado en el expediente.
        </Typography.Paragraph>
        <Input.TextArea
          rows={4}
          value={motivo}
          onChange={(e) => setMotivo(e.target.value)}
          placeholder="Ej.: Requisitos técnicos incompatibles con el alcance, plazo de entrega inviable o márgenes fuera de política."
          maxLength={4000}
          showCount
          data-testid="input-motivo-no-go"
        />
        {motivo.trim().length > 0 && motivo.trim().length < MOTIVO_MIN && (
          <Alert
            type="warning"
            showIcon
            style={{ marginTop: 8 }}
            message={`El motivo debe tener al menos ${MOTIVO_MIN} caracteres`}
          />
        )}
      </Modal>
    </div>
  );
}
