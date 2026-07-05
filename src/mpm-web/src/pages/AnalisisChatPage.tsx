import { Card, Button, Space, Typography } from 'antd'
import { ArrowLeftOutlined, RobotOutlined } from '@ant-design/icons'
import { useNavigate, useParams } from 'react-router-dom'
import { AnalisisChat } from '../components/AnalisisChat'
import { useDashboard } from '../hooks/useAnalisis'

/**
 * Vista dedicada del chat contextual de un análisis:
 * misma lógica conversacional que el dashboard, con más espacio
 * para leer el historial y hacer consultas.
 */
export function AnalisisChatPage() {
  const { id } = useParams<{ id: string }>()
  const workspaceId = id ? Number(id) : null
  const navigate = useNavigate()

  const { data: dashboardData } = useDashboard(workspaceId)
  const resultado = dashboardData?.data
  let nombreLicitacion: string | undefined
  try {
    nombreLicitacion = resultado?.contenidoJson
      ? (JSON.parse(resultado.contenidoJson)?.licitacion?.nombre as string | undefined)
      : undefined
  } catch {
    nombreLicitacion = undefined
  }

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div className="mpm-page-header">
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <Button
            icon={<ArrowLeftOutlined />}
            onClick={() => navigate(`/analisis/${workspaceId}/dashboard`)}
            style={{ borderRadius: 10, height: 36 }}
          >
            Volver al dashboard
          </Button>
          <div
            style={{
              width: 32,
              height: 32,
              borderRadius: 8,
              background: 'linear-gradient(135deg, #8b5cf6, #a78bfa)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              boxShadow: '0 4px 10px rgba(139,92,246,0.3)',
            }}
          >
            <RobotOutlined style={{ color: 'white', fontSize: 15 }} />
          </div>
          <div>
            <h1 className="mpm-page-title" style={{ fontSize: 18, margin: 0 }}>
              Consultas sobre el análisis
            </h1>
            {nombreLicitacion && (
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {nombreLicitacion}
              </Typography.Text>
            )}
          </div>
        </div>
      </div>

      <Card>
        <AnalisisChat workspaceId={workspaceId} maxHeight="calc(100vh - 340px)" />
      </Card>
    </Space>
  )
}
