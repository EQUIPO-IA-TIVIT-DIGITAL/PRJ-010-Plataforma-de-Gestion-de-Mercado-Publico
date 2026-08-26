import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPut } from '../lib/apiClient';

export interface PreferenciasLicitaciones {
  montoMinimo: number | null;
}

export interface PreferenciasLicitacionesUpdate {
  montoMinimo: number | null;
}

const BASE = '/api/v1/usuarios/me/preferencias-licitaciones';

/**
 * F1-T5: Hook de lectura de preferencia monto mínimo.
 * GET /api/v1/usuarios/me/preferencias-licitaciones
 * Envelope backend: { success: true, data: { montoMinimo: number|null } }
 * staleTime 5 min per spec (preferencia estable por sesión).
 */
export function usePreferenciasLicitaciones() {
  return useQuery({
    queryKey: ['preferencias-licitaciones'],
    queryFn: () => apiGet<{ data: PreferenciasLicitaciones }>(BASE),
    staleTime: 5 * 60 * 1000,
    retry: 1,
  });
}

/**
 * Mutation lista para futuro (PUT /api/v1/usuarios/me/preferencias-licitaciones).
 * No requerida en F1-T5 pero deja el hook preparado.
 * Uso: useActualizarPreferenciaLicitaciones().mutate({ montoMinimo: 50000000 })
 */
export function useActualizarPreferenciaLicitaciones() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: PreferenciasLicitacionesUpdate) =>
      apiPut<{ data: PreferenciasLicitaciones }>(BASE, body),
    onSuccess: (data) => {
      queryClient.setQueryData(['preferencias-licitaciones'], data);
      queryClient.invalidateQueries({ queryKey: ['preferencias-licitaciones'] });
    },
  });
}

/** Alias compat para espec futuro: useActualizarPreferencia */
export const useActualizarPreferencia = useActualizarPreferenciaLicitaciones;
