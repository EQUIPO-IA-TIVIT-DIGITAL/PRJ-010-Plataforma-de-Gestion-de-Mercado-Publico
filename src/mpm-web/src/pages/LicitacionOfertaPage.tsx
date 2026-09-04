import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  Button,
  Card,
  Descriptions,
  Divider,
  Empty,
  Space,
  Spin,
  Tabs,
  Tag,
  Typography,
  theme,
} from 'antd';
import {
  ArrowLeftOutlined,
  FilePdfOutlined,
  RobotOutlined,
  TeamOutlined,
  CheckCircleOutlined,
  FileWordOutlined,
  GlobalOutlined,
} from '@ant-design/icons';
import { useLicitacionDetalle } from '../hooks/useLicitacionDetalle';
import { useDecision } from '../hooks/useCenso';
import { useEstadoDocumentos } from '../hooks/useDocumentosLicitacion';
import { StatusBadge } from '../components/StatusBadge';
import type { StatusBadgeVariant } from '../components/StatusBadge';
import { DocumentosLicitacionPanel } from '../components/DocumentosLicitacionPanel';
import { AnalisisComercialPanel } from '../components/AnalisisComercialPanel';
import { CapacidadesTIVITPanel } from '../components/CapacidadesTIVITPanel';
import { DecisionGoNoGoPanel } from '../components/DecisionGoNoGoPanel';
import { PropuestaPanel } from '../components/PropuestaPanel';

const ESTADO_VARIANT: Record<number, StatusBadgeVariant> = {
  1: 'info',
  2: 'warning',
  3: 'neutral',
  4: 'error',
  5: 'success',
  6: 'neutral',
  7: 'tertiary',
  8: 'warning',
};

function formatDate(d: string | null): string {
  if (!d) return '-';
  return new Intl.DateTimeFormat('es-CL', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(d));
}

function formatCurrency(v: number | null): string {
  if (v == null) return '-';
  return new Intl.NumberFormat('es-CL', { style: 'currency', currency: 'CLP', maximumFractionDigits: 0 }).format(v);
}

export function LicitacionOfertaPage() {
  const { codigo } = useParams<{ codigo: string }>();
  const navigate = useNavigate();
  const { token } = theme.useToken();
  const [activeTab, setActiveTab] = useState('1');

  const { data: response, isLoading, error } = useLicitacionDetalle(codigo ?? null);
  const licitacion = response?.data;
  const { data: decisionData } = useDecision(codigo ?? null);
  const decision = decisionData?.data;
  const { data: estadoDocs } = useEstadoDocumentos(codigo ?? null);
  const numDocs = estadoDocs?.data?.documentos?.length ?? 0;

  if (isLoading) {
    return (
      <div style={{ textAlign: 'center', padding: '100px 0' }}>
        <Spin size="large" tip="Cargando Espacio Comercial de la Licitación..." />
      </div>
    );
  }

  if (error || !licitacion || !codigo) {
    return (
      <div style={{ padding: 24 }}>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/licitaciones')} style={{ marginBottom: 16 }}>
          Volver a Licitaciones
        </Button>
        <Empty description="Licitación no encontrada o no disponible" />
      </div>
    );
  }

  const tabItems = [
    {
      key: '1',
      label: (
        <span>
          <FilePdfOutlined /> 1. Bases y Pliegos
          {numDocs > 0 && <Tag color="blue" style={{ marginLeft: 6 }}>{numDocs}</Tag>}
        </span>
      ),
      children: <DocumentosLicitacionPanel codigoExterno={codigo} onIrAAnalisis={() => setActiveTab('2')} />,
    },
    {
      key: '2',
      label: (
        <span>
          <RobotOutlined /> 2. Análisis IA
        </span>
      ),
      children: <AnalisisComercialPanel codigoExterno={codigo} />,
    },
    {
      key: '3',
      label: (
        <span>
          <TeamOutlined /> 3. Capacidades TIVIT
        </span>
      ),
      children: <CapacidadesTIVITPanel codigoExterno={codigo} />,
    },
    {
      key: '4',
      label: (
        <span>
          <CheckCircleOutlined /> 4. Decisión GO/NO GO
          {decision?.decision === 'go' && <Tag color="success" style={{ marginLeft: 6 }}>GO</Tag>}
          {decision?.decision === 'no_go' && <Tag color="error" style={{ marginLeft: 6 }}>NO GO</Tag>}
        </span>
      ),
      children: <DecisionGoNoGoPanel codigoExterno={codigo} />,
    },
    {
      key: '5',
      label: (
        <span>
          <FileWordOutlined /> 5. Propuesta Comercial & Drive
        </span>
      ),
      children: <PropuestaPanel codigoExterno={codigo} onIrADecision={() => setActiveTab('4')} />,
    },
  ];

  return (
    <div style={{ padding: '0 8px 32px 8px', maxWidth: 1400, margin: '0 auto' }}>
      {/* Header Bar */}
      <div style={{ marginBottom: 16, display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 12 }}>
        <Space size={12}>
          <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/licitaciones')}>
            Licitaciones
          </Button>
          <div>
            <Typography.Title level={4} style={{ margin: 0, lineHeight: 1.2 }}>
              Sala de Oferta: {licitacion.codigoExterno}
            </Typography.Title>
            <Typography.Text type="secondary" style={{ fontSize: 13 }}>
              {licitacion.nombre}
            </Typography.Text>
          </div>
        </Space>

        <Space size={8}>
          {decision?.decision ? (
            <Tag color={decision.decision === 'go' ? 'success' : 'error'} style={{ fontSize: 13, padding: '4px 10px' }}>
              Decisión: {decision.decision.toUpperCase()}
            </Tag>
          ) : (
            <Tag color="default" style={{ fontSize: 13, padding: '4px 10px' }}>
              Sin decisión formal
            </Tag>
          )}
          {licitacion.link && (
            <Button
              icon={<GlobalOutlined />}
              href={licitacion.link}
              target="_blank"
              rel="noopener noreferrer"
            >
              Mercado Público
            </Button>
          )}
        </Space>
      </div>

      {/* Executive Summary Card */}
      <Card size="small" style={{ marginBottom: 20, background: token.colorFillAlter, borderColor: token.colorBorderSecondary }}>
        <Descriptions size="small" column={{ xxl: 4, xl: 4, lg: 2, md: 2, sm: 1, xs: 1 }}>
          <Descriptions.Item label="Organismo">{licitacion.organismo}</Descriptions.Item>
          <Descriptions.Item label="Estado">
            <StatusBadge variant={ESTADO_VARIANT[licitacion.estado?.codigo] ?? 'neutral'} label={licitacion.estado?.nombre} />
          </Descriptions.Item>
          <Descriptions.Item label="Monto Estimado">{formatCurrency(licitacion.montoEstimado)}</Descriptions.Item>
          <Descriptions.Item label="Cierre">{formatDate(licitacion.fechaCierre)}</Descriptions.Item>
        </Descriptions>
      </Card>

      {/* Main Stepped Tabs Workspace */}
      <Card style={{ minHeight: 600 }}>
        <Tabs
          type="card"
          activeKey={activeTab}
          onChange={setActiveTab}
          items={tabItems}
          size="middle"
        />
      </Card>
    </div>
  );
}
