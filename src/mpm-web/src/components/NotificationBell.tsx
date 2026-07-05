import { useEffect, useRef, useState } from 'react'
import { Badge, Tooltip, List, Typography, Button, Spin, Empty } from 'antd'
import { BellOutlined, CheckOutlined } from '@ant-design/icons'
import { useNotificacionesLista, useNotificacionesNoLeidasCount, useMarcarLeida, useMarcarTodasLeidas } from '../hooks/useNotificaciones'

const { Text, Paragraph } = Typography

export default function NotificationBell() {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  const { data: countData } = useNotificacionesNoLeidasCount()
  const { data: listData, isLoading } = useNotificacionesLista(1, 10, true)
  const marcarLeida = useMarcarLeida()
  const marcarTodas = useMarcarTodasLeidas()

  const noLeidas = countData?.data?.count ?? 0
  const notificaciones = listData?.data?.items ?? []

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    if (open) document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [open])

  return (
    <div ref={ref} style={{ position: 'relative' }}>
      <Tooltip title="Notificaciones">
        <div
          onClick={() => setOpen(!open)}
          style={{
            width: 38,
            height: 38,
            borderRadius: 10,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            cursor: 'pointer',
            color: 'var(--text-secondary)',
            fontSize: 18,
            transition: 'all 0.15s',
            border: '1px solid var(--border)',
          }}
          onMouseEnter={(e) => {
            e.currentTarget.style.background = 'var(--bg-muted)'
            e.currentTarget.style.color = 'var(--text-primary)'
          }}
          onMouseLeave={(e) => {
            e.currentTarget.style.background = 'transparent'
            e.currentTarget.style.color = 'var(--text-secondary)'
          }}
        >
          <Badge count={noLeidas} size="small">
            <BellOutlined style={{ fontSize: 16, color: 'inherit' }} />
          </Badge>
        </div>
      </Tooltip>

      {open && (
        <div
          style={{
            position: 'absolute',
            top: 44,
            right: 0,
            width: 380,
            maxHeight: 480,
            background: '#fff',
            borderRadius: 12,
            boxShadow: '0 8px 24px rgba(0,0,0,0.12)',
            border: '1px solid #e8e8e8',
            zIndex: 1050,
            display: 'flex',
            flexDirection: 'column',
          }}
        >
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              padding: '12px 16px',
              borderBottom: '1px solid #f0f0f0',
            }}
          >
            <Text strong style={{ fontSize: 14 }}>Notificaciones</Text>
            {noLeidas > 0 && (
              <Button
                type="text"
                size="small"
                icon={<CheckOutlined />}
                onClick={() => marcarTodas.mutateAsync()}
                loading={marcarTodas.isPending}
              >
                Marcar todas leídas
              </Button>
            )}
          </div>

          <div style={{ flex: 1, overflow: 'auto', minHeight: 100 }}>
            {isLoading ? (
              <div style={{ textAlign: 'center', padding: 24 }}><Spin /></div>
            ) : notificaciones.length === 0 ? (
              <Empty description="Sin notificaciones" style={{ padding: 24 }} />
            ) : (
              <List
                dataSource={notificaciones}
                renderItem={(item) => (
                  <List.Item
                    style={{
                      padding: '10px 16px',
                      cursor: 'pointer',
                      background: item.leido ? 'transparent' : '#f6f8ff',
                      borderBottom: '1px solid #f5f5f5',
                    }}
                    onClick={() => {
                      if (!item.leido) marcarLeida.mutate(item.id)
                    }}
                  >
                    <List.Item.Meta
                      title={
                        <Text strong style={{ fontSize: 13 }}>
                          {item.titulo}
                        </Text>
                      }
                      description={
                        <Paragraph
                          ellipsis={{ rows: 2 }}
                          style={{ margin: 0, fontSize: 12, color: '#666' }}
                        >
                          {item.mensaje}
                        </Paragraph>
                      }
                    />
                  </List.Item>
                )}
              />
            )}
          </div>
        </div>
      )}
    </div>
  )
}
