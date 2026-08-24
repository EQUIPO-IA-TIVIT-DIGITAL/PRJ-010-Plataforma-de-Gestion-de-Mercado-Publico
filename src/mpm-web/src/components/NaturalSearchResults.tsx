import { List, Card, Tag, Empty, Spin, Typography, Tooltip } from 'antd';
import { EnvironmentOutlined, DollarOutlined } from '@ant-design/icons';
import type { LicitacionNaturalSearchResult } from '../types/licitacion';

const { Text, Paragraph } = Typography;

const ESTADO_LABEL: Record<number, { label: string; color: string }> = {
  5: { label: 'Publicada', color: 'blue' },
  6: { label: 'Cerrada', color: 'default' },
  7: { label: 'Desierta', color: 'purple' },
  8: { label: 'Adjudicada', color: 'green' },
  15: { label: 'Revocada', color: 'red' },
};

interface Props {
  results: LicitacionNaturalSearchResult[];
  loading: boolean;
  query: string;
  onSelect: (codigoExterno: string) => void;
}

function formatDate(d: string | null): string {
  if (!d) return '—';
  return new Intl.DateTimeFormat('es-CL', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(d));
}

const formatCLP = (value: number | null | undefined): string => {
  if (value === null || value === undefined) return '—';
  return new Intl.NumberFormat('es-CL', {
    style: 'currency',
    currency: 'CLP',
    minimumFractionDigits: 0,
  }).format(value);
};

// 018-buscador-inteligente-nl US3 (FR-004): muestra solo el resumen que ya viene en la
// respuesta de buscar-natural -- nunca dispara una descarga de adjuntos al renderizar la
// lista. Los documentos completos solo se cargan si el usuario abre el detalle (onSelect).
export function NaturalSearchResults({ results, loading, query, onSelect }: Props) {
  if (loading) {
    return (
      <div style={{ textAlign: 'center', padding: '48px 0' }}>
        <Spin />
      </div>
    );
  }

  if (query.trim().length < 2) {
    return (
      <Empty
        description="Escribe una consulta en lenguaje natural, por ejemplo: 'ciberseguridad para el sector salud'"
        style={{ padding: '32px 0' }}
      />
    );
  }

  if (results.length === 0) {
    return (
      <Empty
        description={`Sin resultados relevantes para "${query}"`}
        style={{ padding: '32px 0' }}
      />
    );
  }

  return (
    <List
      dataSource={results}
      renderItem={item => {
        const estado = ESTADO_LABEL[item.codigoEstado];
        return (
          <List.Item style={{ padding: 0, marginBottom: 10 }}>
            <Card
              hoverable
              size="small"
              style={{ width: '100%' }}
              onClick={() => onSelect(item.codigoExterno)}
              data-testid="natural-search-result"
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <Text strong>{item.nombre}</Text>
                  <Paragraph type="secondary" ellipsis={{ rows: 2 }} style={{ marginBottom: 4, fontSize: 13 }}>
                    {item.descripcion || 'Sin descripción disponible'}
                  </Paragraph>
                  <Text type="secondary" style={{ fontSize: 12 }}>
                    <EnvironmentOutlined /> {item.organismo || 'Organismo no informado'} · {item.codigoExterno} · Publicada: {formatDate(item.fechaPublicacion)}
                  </Text>
                </div>
                <div style={{ textAlign: 'right', flexShrink: 0 }}>
                  {estado && <Tag color={estado.color}>{estado.label}</Tag>}
                  <Tooltip title="Relevancia de la búsqueda semántica">
                    <div style={{ fontSize: 11, color: '#94a3b8', marginTop: 4 }}>
                      relevancia {(item.relevancia * 100).toFixed(0)}%
                    </div>
                  </Tooltip>
                </div>
              </div>
            </Card>
          </List.Item>
        );
      }}
    />
  );
}
