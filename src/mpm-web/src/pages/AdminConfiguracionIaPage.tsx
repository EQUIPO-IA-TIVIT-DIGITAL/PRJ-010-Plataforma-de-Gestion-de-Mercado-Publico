import { useState } from 'react'
import { Card, Descriptions, Switch, Space, Tag, Typography, message, Modal, Input, Alert, Spin } from 'antd'
import { CloudServerOutlined, GoogleOutlined, SafetyCertificateOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { useAiProviderSettings, useActualizarAiProvider } from '../hooks/useSystemConfig'
import type { AiProvider } from '../types/systemConfig'

const { Title, Text } = Typography

/**
 * 033-migracion-qwen-g4 (US4): switch del super admin entre gcloud (Gemini) y qwen (Qwen G4).
 * El cambio persiste en la BD y aplica al análisis siguiente sin reiniciar el servicio.
 */
export default function AdminConfiguracionIaPage() {
  const { data, isLoading, isError } = useAiProviderSettings()
  const mutation = useActualizarAiProvider()

  const settings = data?.data
  const [modalOpen, setModalOpen] = useState(false)
  const [pendingProvider, setPendingProvider] = useState<AiProvider | null>(null)
  const [endpoint, setEndpoint] = useState('')
  const [model, setModel] = useState('')

  const confirmChange = (provider: AiProvider) => {
    setPendingProvider(provider)
    // Valores sugeridos: para qwen se pide endpoint/modelo; para gcloud se restaura el default.
    setEndpoint(provider === 'openai' ? (settings?.endpoint ?? '') : '')
    setModel(provider === 'openai' ? (settings?.model === 'gemini-2.5-pro' ? 'qwen3.7-g4' : settings?.model ?? 'qwen3.7-g4') : 'gemini-2.5-pro')
    setModalOpen(true)
  }

  const applyChange = async () => {
    if (!pendingProvider) return
    if (pendingProvider === 'openai' && !endpoint.trim()) {
      message.error('Ingresa la URL del servidor Qwen (la entrega el equipo proveedor)')
      return
    }
    if (!model.trim()) {
      message.error('Ingresa el identificador del modelo')
      return
    }

    try {
      await mutation.mutateAsync({
        provider: pendingProvider,
        endpoint: pendingProvider === 'openai' ? endpoint.trim() : null,
        model: model.trim(),
      })
      message.success(
        pendingProvider === 'openai'
          ? 'Proveedor cambiado a Qwen. El análisis siguiente usará el nuevo modelo.'
          : 'Proveedor cambiado a Google (gcloud). El análisis siguiente usará Gemini.'
      )
      setModalOpen(false)
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'No se pudo cambiar el proveedor')
    }
  }

  if (isLoading) {
    return <div style={{ padding: 40, textAlign: 'center' }}><Spin size="large" /></div>
  }

  if (isError || !settings) {
    return (
      <div style={{ padding: 24 }}>
        <Alert type="error" showIcon message="No se pudo leer la configuración del proveedor de IA" />
      </div>
    )
  }

  const esQwen = settings.provider === 'openai'

  return (
    <div style={{ padding: 24, maxWidth: 900, margin: '0 auto' }}>
      <Title level={3} style={{ marginBottom: 4 }}>Configuración del proveedor de IA</Title>
      <Text type="secondary" style={{ display: 'block', marginBottom: 24 }}>
        Controla qué motor de inteligencia artificial usa el sistema para analizar licitaciones.
        El cambio aplica al siguiente análisis, sin reiniciar el servicio.
      </Text>

      <Card
        title={
          <Space>
            <CloudServerOutlined />
            Proveedor activo
          </Space>
        }
        style={{ marginBottom: 24 }}
      >
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
          <Space size="large" align="center">
            <Space direction="vertical" align="center" size={4}>
              <GoogleOutlined style={{ fontSize: 28, opacity: esQwen ? 0.35 : 1 }} />
              <Text strong type={esQwen ? 'secondary' : undefined}>Google (gcloud)</Text>
              <Text type="secondary">Gemini</Text>
            </Space>
            <Switch
              checked={esQwen}
              onChange={(checked) => confirmChange(checked ? 'openai' : 'gemini')}
              loading={mutation.isPending}
              checkedChildren="Qwen"
              unCheckedChildren="Gemini"
              size="large"
            />
            <Space direction="vertical" align="center" size={4}>
              <SafetyCertificateOutlined style={{ fontSize: 28, opacity: esQwen ? 1 : 0.35 }} />
              <Text strong={esQwen}>Qwen (infraestructura privada)</Text>
              <Text type="secondary">Qwen 3.7 G4</Text>
            </Space>
          </Space>

          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label="Proveedor activo">
              <Tag color={esQwen ? 'purple' : 'blue'}>{settings.provider}</Tag>
            </Descriptions.Item>
            <Descriptions.Item label="Modelo">{settings.model}</Descriptions.Item>
            {settings.endpoint && <Descriptions.Item label="Endpoint">{settings.endpoint}</Descriptions.Item>}
            <Descriptions.Item label="Origen de la configuración">
              {settings.resolvedFrom === 'database' ? 'Selección guardada (switch)' : 'Variables de entorno'}
            </Descriptions.Item>
            {settings.updatedByUsername && (
              <Descriptions.Item label="Último cambio">
                {settings.updatedByUsername} · {settings.updatedAt ? dayjs(settings.updatedAt).format('DD/MM/YYYY HH:mm') : ''}
              </Descriptions.Item>
            )}
          </Descriptions>
        </Space>
      </Card>

      <Modal
        title={pendingProvider === 'openai' ? 'Cambiar a Qwen (infraestructura privada)' : 'Cambiar a Google (gcloud)'}
        open={modalOpen}
        onOk={applyChange}
        confirmLoading={mutation.isPending}
        onCancel={() => setModalOpen(false)}
        okText="Confirmar cambio"
        cancelText="Cancelar"
      >
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message="El cambio aplica a los análisis siguientes"
          description="Los análisis en curso terminan con el proveedor con el que empezaron. Puedes volver a cambiar en cualquier momento."
        />
        {pendingProvider === 'openai' && (
          <Input
            placeholder="URL del servidor Qwen (ej. https://qwen.tivit.internal/v1)"
            value={endpoint}
            onChange={(e) => setEndpoint(e.target.value)}
            style={{ marginBottom: 12 }}
          />
        )}
        <Input
          placeholder="Identificador del modelo"
          value={model}
          onChange={(e) => setModel(e.target.value)}
        />
      </Modal>
    </div>
  )
}
