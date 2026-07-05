import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import type {
  WorkspaceItem,
  WorkspaceDetalle,
  DocumentoItem,
  ResultadoAnalisis,
  AnalisisResumen,
  ChatResponse,
  ChatHistorial,
  DashboardEjecutivo,
} from '../types/analisis'

import { apiFetch } from '../lib/apiClient'

const BASE = '/api/v1/analisis'

export function useWorkspacesLista(page = 1, pageSize = 20, search?: string, estado?: string) {
  const params = new URLSearchParams()
  params.set('page', String(page))
  params.set('pageSize', String(pageSize))
  if (search) params.set('search', search)
  if (estado) params.set('estado', estado)

  return useQuery({
    queryKey: ['analisis-workspaces', { page, pageSize, search, estado }],
    queryFn: () => apiFetch<{ data: { items: WorkspaceItem[]; totalRecords: number; totalPages: number; page: number; pageSize: number } }>(`${BASE}/workspaces?${params.toString()}`),
    staleTime: 10_000,
    refetchInterval: 5000,
  })
}

export function useWorkspaceDetalle(id: number | null) {
  return useQuery({
    queryKey: ['analisis-workspace', id],
    queryFn: () => apiFetch<{ data: WorkspaceDetalle }>(`${BASE}/workspaces/${id}`),
    enabled: !!id,
    refetchInterval: (query) => {
      const estado = query.state.data?.data?.estado
      return estado === 'analizando' ? 3000 : false
    },
    refetchIntervalInBackground: true,
  })
}

export function useCrearWorkspace() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: { licitacionId?: number; nombre: string }) =>
      apiFetch<{ data: WorkspaceDetalle }>(`${BASE}/workspaces`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['analisis-workspaces'] })
    },
  })
}

export function useEliminarWorkspace() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) =>
      apiFetch<{ data: { result: boolean } }>(`${BASE}/workspaces/${id}`, { method: 'DELETE' }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['analisis-workspaces'] })
    },
  })
}

export function useListarDocumentos(workspaceId: number | null) {
  return useQuery({
    queryKey: ['analisis-documentos', workspaceId],
    queryFn: () => apiFetch<{ data: DocumentoItem[] }>(`${BASE}/workspaces/${workspaceId}/documentos`),
    enabled: !!workspaceId,
  })
}

export function useSubirDocumento() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ workspaceId, archivo }: { workspaceId: number; archivo: File }) => {
      const formData = new FormData()
      formData.append('archivo', archivo)
      return apiFetch<{ data: WorkspaceDetalle }>(`${BASE}/workspaces/${workspaceId}/documentos`, {
        method: 'POST',
        body: formData,
      })
    },
    onSuccess: (_, { workspaceId }) => {
      queryClient.invalidateQueries({ queryKey: ['analisis-documentos', workspaceId] })
      queryClient.invalidateQueries({ queryKey: ['analisis-workspace', workspaceId] })
    },
  })
}

export function useAnalizar() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ workspaceId, documentoId }: { workspaceId: number; documentoId?: number }) =>
      apiFetch<{ data: AnalisisResumen }>(`${BASE}/workspaces/${workspaceId}/analizar`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ documentoId }),
      }),
    onSuccess: (_, { workspaceId }) => {
      queryClient.invalidateQueries({ queryKey: ['analisis-dashboard', workspaceId] })
      queryClient.invalidateQueries({ queryKey: ['analisis-workspace', workspaceId] })
      queryClient.invalidateQueries({ queryKey: ['analisis-workspaces'] })
    },
  })
}

export function useDashboard(workspaceId: number | null) {
  return useQuery({
    queryKey: ['analisis-dashboard', workspaceId],
    queryFn: () => apiFetch<{ data: ResultadoAnalisis }>(`${BASE}/workspaces/${workspaceId}/dashboard`),
    enabled: !!workspaceId,
    staleTime: 60_000,
  })
}

export function useEnviarChat() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ workspaceId, mensaje }: { workspaceId: number; mensaje: string }) =>
      apiFetch<{ data: ChatResponse }>(`${BASE}/workspaces/${workspaceId}/chat`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ mensaje }),
      }),
    onSuccess: (_, { workspaceId }) => {
      queryClient.invalidateQueries({ queryKey: ['analisis-chat', workspaceId] })
    },
  })
}

export function useChatHistorial(workspaceId: number | null) {
  return useQuery({
    queryKey: ['analisis-chat', workspaceId],
    queryFn: () => apiFetch<{ data: ChatHistorial }>(`${BASE}/workspaces/${workspaceId}/chat`),
    enabled: !!workspaceId,
  })
}

export function useEjecutivoDashboard(anio?: number | null) {
  const params = new URLSearchParams()
  if (anio) params.set('anio', String(anio))
  return useQuery({
    queryKey: ['analisis-ejecutivo', anio],
    queryFn: () => apiFetch<{ data: DashboardEjecutivo }>(`${BASE}/ejecutivo?${params.toString()}`),
    staleTime: 30_000,
  })
}
