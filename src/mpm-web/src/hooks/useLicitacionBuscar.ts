import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../lib/apiClient';

async function buscarLicitaciones(q: string, limit: number = 10) {
  return apiGet<unknown>(`/api/v1/licitaciones/buscar?q=${encodeURIComponent(q)}&limit=${limit}`);
}

export function useLicitacionBuscar(q: string, limit: number = 10) {
  return useQuery({
    queryKey: ['licitaciones-buscar', q, limit],
    queryFn: () => buscarLicitaciones(q, limit),
    enabled: q.length >= 3,
    staleTime: 30_000,
    retry: 1,
  });
}
