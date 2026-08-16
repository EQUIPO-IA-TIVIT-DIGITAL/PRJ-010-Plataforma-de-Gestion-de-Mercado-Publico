import { useState } from 'react';
import { Alert, App as AntdApp, Button, Input, Modal, Space, Spin, Tag, Typography } from 'antd';
import { CheckCircleOutlined, CloseCircleOutlined, ReloadOutlined, RobotOutlined } from '@ant-design/icons';
import { useAnalisisComercialEstado } from '../hooks/useAnalisisComercial';
import { useDecision, useRegistrarDecision } from '../hooks/useCenso';
import { StatusBadge } from './StatusBadge';
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
    strong_go: { color: 'success', label: 'GO fuerte' },
    go: { color: 'green', label: 'GO' },
    no_go: { color: 'orange', label: 'NO GO' },
    strong_no_go: { color: 'error', label: 'NO GO fuerte' },
  };
  const m = map[go] ?? { color: 'default', label: go };
  return <Tag color={m.color}>{m.label}</Tag>;
}

/**
 * 036-flujo-comercial-ofertas (Fase 2): decisión formal GO/NO GO del gerente.
 * La IA solo recomienda (vive en el análisis comercial); la decisión es siempre humana.
 */
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
  // Recomendación IA en vivo (del análisis comercial); si no existe, la decisión queda sin respaldo IA.
  const recomendacion = analisis?.estado === 'completado' ? analisis.goNoGo : null;
  const confianza = analisis?.estado === 'completado' ? analisis.scoreConfianza : null;

  const enviarDecision = (valor: 'go' | 'no_go', mot: string | null) => {
    if (!codigoExterno) return;
    registrar.mutate(
      { codigoExterno, decision: valor, motivo: mot ?? undefined },
      {
        onSuccess: () => {
          message.success(valor === 'go' ? 'Decisión GO registrada: TIVIT ofertará esta licitación' : 'Decisión NO GO registrada: TIVIT no ofertará esta licitación');
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
      title: 'Registrar decisión GO',
      icon: <CheckCircleOutlined style={{ color: '#52c41a' }} />,
      content: 'TIVIT ofertará esta licitación y habilita la generación de la propuesta (Fase 3). ¿Confirmas la decisión GO?',
      okText: 'Sí, registrar GO',
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
    <Space wrap>
      <Button
        type="primary"
        icon={<CheckCircleOutlined />}
        loading={registrar.isPending}
        disabled={!codigoExterno}
        onClick={confirmarGo}
        data-testid="btn-decision-go"
      >
        GO — ofertar
      </Button>
      <Button
        danger
        icon={<CloseCircleOutlined />}
        loading={registrar.isPending}
        disabled={!codigoExterno}
        onClick={abrirModalNoGo}
        data-testid="btn-decision-no-go"
      >
        NO GO — no ofertar
      </Button>
      {redecidiendo && (
        <Button size="small" onClick={() => setRedecidiendo(false)}>
          Cancelar
        </Button>
      )}
    </Space>
  );

  const renderDecisionRegistrada = (d: DecisionEstado) => (
    <Space direction="vertical" size={8} style={{ width: '100%' }} data-testid="badge-decision">
      <Space wrap>
        <span data-testid="badge-decision-estado">
          <StatusBadge
            variant={d.decision === 'go' ? 'success' : 'error'}
            label={d.decision === 'go' ? 'GO — TIVIT oferta' : 'NO GO — TIVIT no oferta'}
          />
        </span>
        {d.recomendacionIa && (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Respaldo IA al decidir: {recomendacionTag(d.recomendacionIa)}
            {d.scoreConfianza != null ? ` (${(d.scoreConfianza * 100).toFixed(0)}%)` : ''}
          </Typography.Text>
        )}
      </Space>
      {d.motivo && (
        <Typography.Paragraph style={{ marginBottom: 0 }}>
          <Typography.Text strong>Motivo: </Typography.Text>
          {d.motivo}
        </Typography.Paragraph>
      )}
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        Decidido por {d.decididoPor ?? '-'} el {formatFecha(d.decididoAt)}
      </Typography.Text>
      <Button size="small" onClick={() => setRedecidiendo(true)} data-testid="btn-cambiar-decision">
        Cambiar decisión
      </Button>
    </Space>
  );

  return (
    <>
      <Typography.Title level={5} style={{ marginTop: 24 }}>
        Decisión GO/NO GO
      </Typography.Title>

      <Space wrap style={{ marginBottom: 12 }}>
        <Typography.Text type="secondary">
          <RobotOutlined style={{ marginRight: 6 }} />
          Recomendación IA:
        </Typography.Text>
        {recomendacion ? (
          <Space size={6}>
            {recomendacionTag(recomendacion)}
            {confianza != null && (
              <Typography.Text type="secondary">Confianza: {(confianza * 100).toFixed(0)}%</Typography.Text>
            )}
          </Space>
        ) : (
          <Typography.Text type="secondary">sin recomendación IA (analiza los documentos primero)</Typography.Text>
        )}
      </Space>

      {isLoading && !decision ? (
        <Spin />
      ) : error && !decision ? (
        <Alert
          type="error"
          showIcon
          message="No se pudo consultar la decisión"
          description={error instanceof Error ? error.message : 'Intente nuevamente'}
          action={
            <Button size="small" icon={<ReloadOutlined />} onClick={() => refetch()}>
              Reintentar
            </Button>
          }
        />
      ) : !decision || !decision.decidida || redecidiendo ? (
        <Alert
          type="info"
          showIcon
          message={redecidiendo ? 'Cambiar la decisión registrada' : 'Aún no hay decisión formal'}
          description={
            redecidiendo
              ? 'La nueva decisión reemplaza la anterior (la IA solo recomienda; la decisión es humana).'
              : 'El gerente decide si TIVIT oferta esta licitación. La decisión queda registrada con quién y cuándo.'
          }
          action={renderAcciones()}
        />
      ) : (
        renderDecisionRegistrada(decision)
      )}

      <Modal
        open={modalNoGo}
        title="Registrar decisión NO GO"
        okText="Registrar NO GO"
        okButtonProps={{ danger: true, disabled: motivo.trim().length < MOTIVO_MIN }}
        cancelText="Cancelar"
        onOk={confirmarNoGo}
        onCancel={() => setModalNoGo(false)}
        data-testid="modal-motivo-no-go"
      >
        <Typography.Paragraph>
          TIVIT no ofertará esta licitación. El motivo es obligatorio (mínimo {MOTIVO_MIN} caracteres) y
          queda registrado para el expediente comercial.
        </Typography.Paragraph>
        <Input.TextArea
          rows={4}
          value={motivo}
          onChange={(e) => setMotivo(e.target.value)}
          placeholder="Ej.: Requisitos técnicos exceden las capacidades actuales y el plazo de entrega es inviable."
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
    </>
  );
}
