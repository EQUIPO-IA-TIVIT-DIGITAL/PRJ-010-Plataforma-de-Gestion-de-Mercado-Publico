import { Card, Button, Space } from 'antd'
import { ArrowLeftOutlined, RobotOutlined } from '@ant-design/icons'
import { useNavigate, useParams } from 'react-router-dom'
import { AnalisisChat } from '../components/AnalisisChat'
import { useDashboard } from '../hooks/useAnalisis'
import { PageHeader } from '../components/PageHeader'

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
      <div>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate(`/analisis/${workspaceId}/dashboard`)} type="text" style={{ marginBottom: 8, paddingLeft: 0 }}>
          Volver al dashboard
        </Button>
        <PageHeader icon={<RobotOutlined />} title="Consultas sobre el análisis" subtitle={nombreLicitacion} />
      </div>

      <Card>
        <AnalisisChat workspaceId={workspaceId} maxHeight="calc(100vh - 340px)" />
      </Card>
    </Space>
  )
}
