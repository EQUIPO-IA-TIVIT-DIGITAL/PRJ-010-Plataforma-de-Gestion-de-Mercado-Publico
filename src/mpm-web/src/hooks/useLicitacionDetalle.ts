import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../lib/apiClient';
import type { LicitacionDetalle } from '../types/licitacion';

async function fetchDetalle(codigoExterno: string) {
  return apiGet<{ data?: LicitacionDetalle }>(`/api/v1/licitaciones/${encodeURIComponent(codigoExterno)}`);
}

export function useLicitacionDetalle(codigoExterno: string | null) {
  return useQuery({
    queryKey: ['licitacion', codigoExterno],
    queryFn: () => fetchDetalle(codigoExterno!),
    enabled: !!codigoExterno,
    staleTime: 300_000,
    retry: 1,
  });
}
