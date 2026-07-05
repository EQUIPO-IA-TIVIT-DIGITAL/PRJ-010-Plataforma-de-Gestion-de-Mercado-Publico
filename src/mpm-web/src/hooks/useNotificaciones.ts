import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import type { NotificacionItem, NotificacionesCount } from '../types/notificaciones'

import { apiFetch } from '../lib/apiClient'

const BASE = '/api/v1/notificaciones'

export function useNotificacionesLista(page = 1, pageSize = 20, soloNoLeidas = false) {
  const params = new URLSearchParams()
  params.set('page', String(page))
  params.set('pageSize', String(pageSize))
  if (soloNoLeidas) params.set('soloNoLeidas', 'true')

  return useQuery({
    queryKey: ['notificaciones', { page, pageSize, soloNoLeidas }],
    queryFn: () => apiFetch<{ data: { items: NotificacionItem[]; totalRecords: number; totalPages: number; page: number; pageSize: number } }>(
      `${BASE}?${params}`
    ),
  })
}

export function useNotificacionesNoLeidasCount() {
  return useQuery({
    queryKey: ['notificaciones', 'count'],
    queryFn: () => apiFetch<{ data: NotificacionesCount }>(`${BASE}/no-leidas/count`),
    refetchInterval: 30000,
  })
}

export function useMarcarLeida() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: number) =>
      apiFetch<{ data: unknown }>(`${BASE}/${id}/leer`, { method: 'PUT' }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notificaciones'] })
    },
  })
}

export function useMarcarTodasLeidas() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () =>
      apiFetch<{ data: { marcadas: number } }>(`${BASE}/leer-todas`, { method: 'PUT' }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notificaciones'] })
    },
  })
}

export function useEliminarNotificacion() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: number) =>
      apiFetch<{ data: unknown }>(`${BASE}/${id}`, { method: 'DELETE' }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notificaciones'] })
    },
  })
}

export function useEliminarTodasNotificaciones() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () =>
      apiFetch<{ data: { eliminadas: number } }>(BASE, { method: 'DELETE' }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notificaciones'] })
    },
  })
}
