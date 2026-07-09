import { useMutation, useQuery } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { AnalisisCompetidorResponse, AnalizarCompetidorRequest, OfertaCompetidor } from '../types/competidores';

const BASE = '/api/v1/competidores';

export function useBuscarCompetidor(nombre: string) {
  return useQuery({
    queryKey: ['competidores', nombre],
    queryFn: () => apiGet<{ data: OfertaCompetidor[] }>(`${BASE}?nombre=${encodeURIComponent(nombre)}`),
    enabled: nombre.trim().length >= 2,
  });
}

export function useAnalizarCompetidor() {
  return useMutation({
    mutationFn: (request: AnalizarCompetidorRequest) =>
      apiPost<{ data: AnalisisCompetidorResponse }>(`${BASE}/analisis`, request),
  });
}
