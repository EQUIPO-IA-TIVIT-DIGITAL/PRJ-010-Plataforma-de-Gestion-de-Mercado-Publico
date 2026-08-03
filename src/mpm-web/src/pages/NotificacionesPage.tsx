import { useState, useMemo } from 'react'
import { Typography, Button, Empty, Spin, Popconfirm, Space, Segmented, Pagination } from 'antd'
import { CheckOutlined, BellOutlined, StarFilled, DeleteOutlined, WarningOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc'
import timezone from 'dayjs/plugin/timezone'
import {
  useNotificacionesLista, useMarcarLeida, useMarcarTodasLeidas,
  useEliminarNotificacion, useEliminarTodasNotificaciones,
} from '../hooks/useNotificaciones'
import { useAuth } from '../hooks/useAuth'

dayjs.extend(utc)
dayjs.extend(timezone)

const { Title } = Typography

function parseMeta(metadata: string | null | undefined): Record<string, unknown> | null {
  if (!metadata) return null;
  try { return typeof metadata === 'string' ? JSON.parse(metadata) : metadata; }
  catch { return null; }
}

function getLicitacionUrl(metadata: string | null | undefined): string | null {
  const meta = parseMeta(metadata);
  if (!meta) return null;
  const codigo = meta.codigo_externo as string | undefined;
  return codigo ? `/licitaciones?codigo=${codigo}` : null;
}

export default function NotificacionesPage() {
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [tabFiltro, setTabFiltro] = useState<'todas' | 'no_leidas' | 'scraper' | 'aclaraciones'>('todas')

  const { data, isLoading } = useNotificacionesLista(page, pageSize)
  const { user } = useAuth()
  const marcarLeida = useMarcarLeida()
  const marcarTodas = useMarcarTodasLeidas()
  const eliminar = useEliminarNotificacion()
  const eliminarTodas = useEliminarTodasNotificaciones()

  const notificaciones = data?.data?.items ?? []
  const totalRecords = data?.data?.totalRecords ?? 0

  const isAdmin = useMemo(() => {
    return user?.email === 'admin@tivit.cl' || user?.roles?.includes('SuperAdmin');
  }, [user]);

  // Filter notificaciones in the frontend to make experience interactive and extremely fast
  const filteredNotificaciones = useMemo(() => {
    return notificaciones.filter(n => {
      // Hide scraper notifications completely for non-admin users
      if (!isAdmin && n.tipo.startsWith('scraper')) return false;

      if (tabFiltro === 'no_leidas') return !n.leido;
      if (tabFiltro === 'scraper') return n.tipo.startsWith('scraper');
      if (tabFiltro === 'aclaraciones') return n.tipo === 'aclaracion_detectada';
      return true;
    });
  }, [notificaciones, tabFiltro, isAdmin]);

  const segmentedOptions = useMemo(() => {
    const opts = [
      { label: 'Todas', value: 'todas' },
      { label: 'No leídas', value: 'no_leidas' },
    ];
    if (isAdmin) {
      opts.push({ label: 'Scrapers', value: 'scraper' });
    }
    opts.push({ label: 'Aclaraciones', value: 'aclaraciones' });
    return opts;
  }, [isAdmin]);

  return (
    <div style={{ padding: '8px 0' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20, flexWrap: 'wrap', gap: 12 }}>
        <div>
          <Title level={3} style={{ margin: 0, fontWeight: 700, color: 'var(--text-primary)' }}>Notificaciones</Title>
          <p style={{ margin: '4px 0 0 0', color: 'var(--text-secondary)', fontSize: 13 }}>
            Historial de eventos de scraping, alertas y aclaraciones detectadas por el sistema.
          </p>
        </div>
        <Space>
          <Button
            icon={<CheckOutlined />}
            onClick={() => marcarTodas.mutateAsync()}
            loading={marcarTodas.isPending}
            style={{ borderRadius: 10, height: 38 }}
          >
            Marcar todas leídas
          </Button>
          <Popconfirm
            title="¿Borrar todas las notificaciones?"
            description="Esta acción no se puede deshacer."
            okText="Borrar todas"
            okButtonProps={{ danger: true }}
            cancelText="Cancelar"
            onConfirm={() => eliminarTodas.mutateAsync()}
          >
            <Button
              danger
              icon={<DeleteOutlined />}
              loading={eliminarTodas.isPending}
              data-testid="notif-delete-all"
              style={{ borderRadius: 10, height: 38 }}
            >
              Borrar todas
            </Button>
          </Popconfirm>
        </Space>
      </div>

      {/* Segmented Filter Control */}
      <Segmented
        value={tabFiltro}
        onChange={(v) => { setTabFiltro(v as any); setPage(1); }}
        options={segmentedOptions}
        style={{ marginBottom: 20, background: '#e2e8f0', padding: 3, borderRadius: 10 }}
      />

      {isLoading ? (
        <div style={{ textAlign: 'center', padding: 80 }}><Spin size="large" tip="Cargando notificaciones..." /></div>
      ) : filteredNotificaciones.length === 0 ? (
        <div style={{ background: '#ffffff', borderRadius: 14, border: '1px solid var(--border)', padding: '60px 20px', textAlign: 'center', boxShadow: 'var(--shadow-card)' }}>
          <Empty description={`No tienes notificaciones en la categoría: ${tabFiltro.replace('_', ' ')}`} />
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          {filteredNotificaciones.map((record) => {
            const isError = record.tipo === 'scraper_error' || record.tipo === 'scraper_config_error';
            const isAclaracion = record.tipo === 'aclaracion_detectada';
            
            return (
              <div
                key={record.id}
                onClick={() => {
                  if (!record.leido) marcarLeida.mutate(record.id)
                }}
                style={{
                  background: record.leido ? '#ffffff' : '#f8faff',
                  border: '1px solid var(--border)',
                  borderLeft: `5px solid ${
                    isError
                      ? '#ef4444' // Red for errors
                      : isAclaracion
                      ? '#f59e0b' // Amber for clarifications
                      : '#10b981' // Green for success scraper runs
                  }`,
                  borderRadius: 12,
                  padding: '16px 20px',
                  cursor: 'pointer',
                  transition: 'all 0.15s cubic-bezier(0.4, 0, 0.2, 1)',
                  display: 'flex',
                  alignItems: 'flex-start',
                  gap: 16,
                  boxShadow: record.leido ? 'none' : '0 4px 12px rgba(59,130,246,0.04)',
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.transform = 'translateX(3px)';
                  e.currentTarget.style.borderColor = 'var(--border-strong)';
                  e.currentTarget.style.boxShadow = 'var(--shadow-sm)';
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.transform = 'none';
                  e.currentTarget.style.borderColor = 'var(--border)';
                  e.currentTarget.style.boxShadow = record.leido ? 'none' : '0 4px 12px rgba(59,130,246,0.04)';
                }}
              >
                {/* Status Indicator Icon */}
                <div
                  style={{
                    width: 40,
                    height: 40,
                    borderRadius: 20,
                    background: isError
                      ? 'rgba(239, 68, 68, 0.08)'
                      : isAclaracion
                      ? 'rgba(245, 158, 11, 0.08)'
                      : 'rgba(16, 185, 129, 0.08)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    color: isError
                      ? '#ef4444'
                      : isAclaracion
                      ? '#d97706'
                      : '#10b981',
                    fontSize: 18,
                    flexShrink: 0,
                  }}
                >
                  {isAclaracion ? (
                    <StarFilled />
                  ) : isError ? (
                    <WarningOutlined />
                  ) : (
                    <BellOutlined />
                  )}
                </div>

                {/* Content Area */}
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12 }}>
                    {/* Title */}
                    <div
                      style={{
                        fontWeight: record.leido ? 600 : 700,
                        fontSize: 14.5,
                        color: 'var(--text-primary)',
                      }}
                    >
                      {isAclaracion && getLicitacionUrl(record.metadata) ? (
                        <a
                          href={getLicitacionUrl(record.metadata)!}
                          onClick={(e) => e.stopPropagation()}
                          style={{ color: '#2563eb', textDecoration: 'none', borderBottom: '1px dashed #2563eb' }}
                        >
                          {record.titulo}
                        </a>
                      ) : (
                        record.titulo
                      )}
                    </div>

                    {/* Date and Quick Actions */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexShrink: 0 }}>
                      <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>
                        {dayjs.utc(record.createdAt).tz('America/Santiago').format('DD-MM-YYYY HH:mm')}
                      </span>
                      
                      {/* Unread indicator */}
                      {!record.leido && (
                        <span
                          style={{
                            width: 8,
                            height: 8,
                            borderRadius: '50%',
                            background: '#3b82f6',
                          }}
                        />
                      )}

                      {/* Delete button */}
                      <Popconfirm
                        title="¿Eliminar esta notificación?"
                        okText="Eliminar"
                        cancelText="Cancelar"
                        onConfirm={(e) => {
                          e?.stopPropagation();
                          eliminar.mutate(record.id);
                        }}
                        onCancel={(e) => e?.stopPropagation()}
                      >
                        <Button
                          type="text"
                          danger
                          size="small"
                          icon={<DeleteOutlined />}
                          onClick={(e) => e.stopPropagation()}
                          style={{ opacity: 0.5, padding: '2px 4px' }}
                        />
                      </Popconfirm>
                    </div>
                  </div>

                  {/* Message body */}
                  <p style={{ margin: '6px 0 0 0', fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.45 }}>
                    {record.mensaje}
                  </p>
                </div>
              </div>
            )
          })}

          {/* Pagination */}
          {totalRecords > pageSize && (
            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 12 }}>
              <Pagination
                current={page}
                pageSize={pageSize}
                total={totalRecords}
                onChange={(p) => setPage(p)}
                showSizeChanger={false}
              />
            </div>
          )}
        </div>
      )}
    </div>
  )
}
