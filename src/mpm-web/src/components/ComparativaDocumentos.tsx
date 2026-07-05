import { Card, Table, Tag, Alert, Space, Typography, Empty } from 'antd'
import { FileProtectOutlined, WarningOutlined, CheckCircleOutlined } from '@ant-design/icons'

export interface ValidacionDocumento {
  nombre?: string
  requerido?: boolean
  enviado?: boolean
  observado_en_acta?: string | null
  estado?: string
}

export interface ValidacionInconsistencia {
  documento?: string
  dice_acta?: string
  evidencia?: string
  severidad?: string
}

export interface ValidacionDocumental {
  documentos?: ValidacionDocumento[]
  inconsistencias?: ValidacionInconsistencia[]
  resumen?: string
  coherente?: boolean
}

const ESTADO_CONFIG: Record<string, { color: string; label: string }> = {
  ok: { color: 'green', label: 'OK' },
  faltante: { color: 'orange', label: 'Faltante' },
  inconsistente: { color: 'red', label: 'Inconsistente' },
  sin_informacion: { color: 'default', label: 'Sin información' },
}

const SEVERIDAD_COLOR: Record<string, string> = {
  alta: 'red',
  media: 'orange',
  baja: 'blue',
}

interface Props {
  validacion?: ValidacionDocumental | null
}

/**
 * Comparativa de documentos del resumen del análisis:
 * requeridos vs. enviados vs. observados/faltantes según el acta,
 * destacando las inconsistencias detectadas.
 */
export function ComparativaDocumentos({ validacion }: Props) {
  if (!validacion) {
    return (
      <Card>
        <Space direction="vertical" size={4}>
          <Typography.Text strong style={{ fontSize: 14 }}>
            <FileProtectOutlined style={{ marginRight: 8, color: '#3b82f6' }} />
            Comparativa de documentos
          </Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            Comparativa no disponible para análisis anteriores; vuelve a analizar el documento para generarla.
          </Typography.Text>
        </Space>
      </Card>
    )
  }

  const documentos = validacion.documentos ?? []
  const inconsistencias = validacion.inconsistencias ?? []
  const coherente = validacion.coherente !== false

  return (
    <Card>
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <FileProtectOutlined style={{ color: '#3b82f6', fontSize: 16 }} />
          <span style={{ fontWeight: 700, fontSize: 14 }}>Comparativa de documentos</span>
          {coherente ? (
            <Tag icon={<CheckCircleOutlined />} color="green">Coherente</Tag>
          ) : (
            <Tag icon={<WarningOutlined />} color="red">Con inconsistencias</Tag>
          )}
        </div>

        {validacion.resumen && (
          <Alert
            type={coherente ? 'info' : 'warning'}
            showIcon
            message={validacion.resumen}
            style={{ borderRadius: 10 }}
          />
        )}

        {inconsistencias.length > 0 && (
          <Space direction="vertical" size={8} style={{ width: '100%' }}>
            {inconsistencias.map((inc, i) => (
              <Alert
                key={i}
                type="error"
                showIcon
                icon={<WarningOutlined />}
                style={{ borderRadius: 10 }}
                message={
                  <span>
                    <strong>{inc.documento}</strong>{' '}
                    <Tag color={SEVERIDAD_COLOR[inc.severidad ?? ''] ?? 'default'} style={{ marginLeft: 4 }}>
                      Severidad {inc.severidad ?? '—'}
                    </Tag>
                  </span>
                }
                description={
                  <div style={{ fontSize: 13 }}>
                    <div><strong>El acta dice:</strong> {inc.dice_acta ?? '—'}</div>
                    <div><strong>Evidencia:</strong> {inc.evidencia ?? '—'}</div>
                  </div>
                }
              />
            ))}
          </Space>
        )}

        {documentos.length > 0 ? (
          <Table
            dataSource={documentos.map((d, i) => ({ ...d, key: i }))}
            pagination={false}
            size="small"
            columns={[
              {
                title: 'Documento',
                dataIndex: 'nombre',
                key: 'nombre',
                render: (v: string) => <span style={{ fontWeight: 500 }}>{v ?? '—'}</span>,
              },
              {
                title: 'Requerido',
                dataIndex: 'requerido',
                key: 'requerido',
                align: 'center' as const,
                render: (v?: boolean) => (v == null ? '—' : v ? 'Sí' : 'No'),
              },
              {
                title: 'Enviado',
                dataIndex: 'enviado',
                key: 'enviado',
                align: 'center' as const,
                render: (v?: boolean) => (v == null ? '—' : v ? 'Sí' : 'No'),
              },
              {
                title: 'Según el acta',
                dataIndex: 'observado_en_acta',
                key: 'observado',
                render: (v?: string | null) => v ?? '—',
              },
              {
                title: 'Estado',
                dataIndex: 'estado',
                key: 'estado',
                align: 'center' as const,
                render: (v?: string) => {
                  const cfg = ESTADO_CONFIG[v ?? ''] ?? { color: 'default', label: v ?? '—' }
                  return <Tag color={cfg.color}>{cfg.label}</Tag>
                },
              },
            ]}
          />
        ) : (
          <Empty description="Sin documentos registrados en la validación" image={Empty.PRESENTED_IMAGE_SIMPLE} />
        )}
      </Space>
    </Card>
  )
}
