import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { LicitacionFilter, LicitacionResumen } from '../types/licitacion';
import type { LicitacionNaturalSearchResult } from '../types/licitacion';

interface LicitacionesResponse {
  data?: {
    items: LicitacionResumen[]
    page: number
    pageSize: number
    totalRecords: number
    totalPages: number
  }
}

async function fetchLicitaciones(filter: LicitacionFilter) {
  const params = new URLSearchParams();
  if (filter.page) params.set('page', String(filter.page));
  if (filter.pageSize) params.set('pageSize', String(filter.pageSize));
  if (filter.search) params.set('search', filter.search);
  if (filter.estado) params.set('estado', String(filter.estado));
  if (filter.tipo) params.set('tipo', filter.tipo);
  if (filter.organismo) params.set('organismo', filter.organismo);
  if (filter.fechaDesde) params.set('fechaDesde', filter.fechaDesde);
  if (filter.fechaHasta) params.set('fechaHasta', filter.fechaHasta);
  if (filter.sortBy) params.set('sortBy', filter.sortBy);
  if (filter.sortDir) params.set('sortDir', filter.sortDir);

  return apiGet<LicitacionesResponse>(`/api/v1/licitaciones?${params.toString()}`);
}

export function useLicitaciones(filter: LicitacionFilter) {
  return useQuery({
    queryKey: ['licitaciones', filter],
    queryFn: () => fetchLicitaciones(filter),
    staleTime: 30_000,
  });
}

export function useBuscarNatural(q: string, page = 1, pageSize = 20, estado?: number | null) {
  return useQuery({
    queryKey: ['licitaciones-buscar-natural', q, page, pageSize, estado],
    queryFn: async () => {
      const params = new URLSearchParams();
      params.set('q', q);
      params.set('page', String(page));
      params.set('pageSize', String(pageSize));
      if (estado != null) params.set('estado', String(estado));

      return apiGet<{ data: { items: LicitacionNaturalSearchResult[]; totalRecords: number; totalPages: number; page: number; pageSize: number } }>(`/api/v1/licitaciones/buscar-natural?${params.toString()}`);
    },
    enabled: q.length >= 2,
    staleTime: 15_000,
  });
}

export function useEsSeguida(codigoExterno: string | undefined) {
  return useQuery({
    queryKey: ['licitacion-seguida', codigoExterno],
    queryFn: async () => {
      const json = await apiGet<{ data?: { esSeguida?: boolean } }>(`/api/v1/licitaciones/${codigoExterno}/seguida`);
      return json.data?.esSeguida as boolean ?? false;
    },
    enabled: !!codigoExterno,
    staleTime: 60_000,
  });
}

export function useSeguirToggle() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (codigoExterno: string) => {
      const json = await apiPost<{ data: { codigoExterno: string; accion: string } }>(`/api/v1/licitaciones/${codigoExterno}/seguir`);
      return json.data;
    },
    onSuccess: (_, codigoExterno) => {
      queryClient.invalidateQueries({ queryKey: ['licitacion-seguida', codigoExterno] });
      queryClient.invalidateQueries({ queryKey: ['licitaciones-seguidas'] });
    },
  });
}

export function useLicitacionesSeguidas() {
  return useQuery({
    queryKey: ['licitaciones-seguidas'],
    queryFn: async () => {
      const json = await apiGet<{ data: Array<{
        codigoExterno: string;
        nombre: string;
        codigoEstado: number;
        fechaPublicacion?: string;
        fechaCierre?: string;
        seguidaDesde: string;
      }> }>('/api/v1/licitaciones/seguidas');
      return json.data;
    },
    staleTime: 60_000,
  });
}
