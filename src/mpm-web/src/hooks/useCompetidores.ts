import { useMutation, useQuery } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { ActividadMercadoResponse, AnalisisCompetidorResponse, AnalizarCompetidorRequest, OfertaCompetidor } from '../types/competidores';

const BASE = '/api/v1/competidores';

export function useListarCompetidores() {
  return useQuery({
    queryKey: ['competidores', 'lista'],
    queryFn: () => apiGet<{ data: string[] }>(`${BASE}/lista`),
  });
}

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

// US4 (spec 031): actividad total de mercado -- polling mientras estado === 'generando'
// (mismo patrón que useAnalisisWorkspace para un análisis en curso). Exponemos refetch
// para que la UI pueda reintentar explícitamente cuando el scraper falla (estado 'error').
export function useActividadMercado(
  nombreCompetidor: string | null,
  area: number | null,
  fechaDesde: string,
  fechaHasta: string,
) {
  const query = useQuery({
    queryKey: ['competidores', nombreCompetidor, 'actividad-mercado', area, fechaDesde, fechaHasta],
    queryFn: async () => {
      const params = new URLSearchParams({ fechaDesde, fechaHasta });
      if (area) params.set('area', String(area));
      const json = await apiGet<{ data: ActividadMercadoResponse }>(
        `${BASE}/${encodeURIComponent(nombreCompetidor!)}/actividad-mercado?${params.toString()}`);
      return json.data;
    },
    enabled: !!nombreCompetidor,
    refetchInterval: (query) => (query.state.data?.estado === 'generando' ? 15_000 : false),
  });
  return query;
}
