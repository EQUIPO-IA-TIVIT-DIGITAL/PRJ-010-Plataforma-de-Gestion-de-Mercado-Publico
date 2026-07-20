import { useEffect, useRef } from 'react'
import { App as AntApp } from 'antd'
import { BellOutlined } from '@ant-design/icons'
import { useQuery } from '@tanstack/react-query'
import { apiFetch } from '../lib/apiClient'
import type { WorkspaceItem, WorkspaceDetalle } from '../types/analisis'

const BASE = '/api/v1/analisis'

/**
 * 029-fix-hallazgos-code-review-competidores-alertas (FR-016/US12, QA BUG-004): antes, el
 * seguimiento de la transición "analizando" → "completado" vivía en un `useRef` local a
 * `AnalisisWorkspacePage.tsx` -- si el usuario navegaba a otra página mientras el análisis
 * corría (como el propio mensaje de inicio invita a hacer: "puedes seguir navegando"), el
 * componente se desmontaba y la notificación nunca aparecía.
 *
 * Mismo patrón que `NotificationBell.tsx` (montado en `AppLayout`, siempre activo sin importar
 * la ruta actual): en vez de vigilar UN workspace específico, este componente sondea la lista de
 * workspaces en estado "analizando" (ya filtrable por `useWorkspacesLista`) y detecta cuándo
 * alguno deja de aparecer ahí -- en ese momento consulta su estado final una sola vez para saber
 * si terminó en "completado" o "error", y dispara la notificación global correspondiente.
 * No renderiza nada visible.
 */
export function AnalisisCompletionWatcher() {
  const { notification } = AntApp.useApp()
  const enAnalizandoPrevio = useRef<Map<number, string>>(new Map())
  const primerPoll = useRef(true)

  const { data } = useQuery({
    queryKey: ['analisis-workspaces-watcher', 'analizando'],
    queryFn: () =>
      apiFetch<{ data: { items: WorkspaceItem[] } }>(`${BASE}/workspaces?page=1&pageSize=50&estado=analizando`),
    refetchInterval: 4000,
    refetchIntervalInBackground: true,
  })

  useEffect(() => {
    const actuales = new Map((data?.data.items ?? []).map((w) => [w.id, w.nombre]))

    // Primer poll tras montar: solo establece la línea base, no dispara notificaciones -- si no,
    // cualquier análisis que ya estuviera "analizando" al cargar la app generaría una
    // notificación falsa en cuanto termine, aunque el usuario nunca lo haya iniciado en esta sesión.
    if (primerPoll.current) {
      primerPoll.current = false
      enAnalizandoPrevio.current = actuales
      return
    }

    for (const [workspaceId, nombre] of enAnalizandoPrevio.current) {
      if (actuales.has(workspaceId)) continue // sigue "analizando", nada que reportar todavía

      // Dejó de estar en la lista "analizando" -- consulta una sola vez su estado final.
      apiFetch<{ data: WorkspaceDetalle }>(`${BASE}/workspaces/${workspaceId}`)
        .then(({ data: workspace }) => {
          if (workspace.estado === 'completado') {
            notification.success({
              message: 'Análisis completado',
              description: `El dashboard del workspace "${workspace.nombre}" está listo para revisar.`,
              placement: 'topRight',
              icon: <BellOutlined style={{ color: '#52c41a' }} />,
              duration: 0,
            })
          } else if (workspace.estado === 'error') {
            notification.error({
              message: 'Análisis falló',
              description: `Hubo un error al procesar el workspace "${nombre}". Revisa la consola del API para más detalles.`,
              placement: 'topRight',
              duration: 0,
            })
          }
        })
        .catch(() => {
          /* si la consulta de estado final falla, no se bloquea el resto -- se pierde esta
             notificación puntual en vez de romper el watcher global */
        })
    }

    enAnalizandoPrevio.current = actuales
  }, [data, notification])

  return null
}
