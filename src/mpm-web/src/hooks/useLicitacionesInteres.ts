import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPatch, apiPost } from '../lib/apiClient';
import type { LicitacionInteres, LicitacionInteresListItem } from '../types/colaboracion';
import type { ConversacionDetalle } from '../types/mensajeria';
import type { WorkspaceDetalle } from '../types/analisis';

export function useLicitacionInteres(licitacionId: number | null) {
  return useQuery({
    queryKey: ['licitacion-interes', licitacionId],
    queryFn: async () => {
      try {
        const json = await apiGet<{ data: LicitacionInteres }>(`/api/v1/licitaciones/${licitacionId}/interes`);
        return json.data;
      } catch {
        return null; // COL_001: no marcada de interés todavía -- no es un error para la UI
      }
    },
    enabled: !!licitacionId,
    staleTime: 15_000,
  });
}

export function useLicitacionesInteresListado() {
  return useQuery({
    queryKey: ['licitaciones-interes'],
    queryFn: async () => {
      const json = await apiGet<{ data: LicitacionInteresListItem[] }>('/api/v1/licitaciones/interes');
      return json.data;
    },
    staleTime: 30_000,
  });
}

// Orquesta los 3 pasos del contrato (contracts/colaboracion-interes.md): marcar interés,
// crear/reusar el análisis, crear la conversación grupal, y persistir el vínculo -- cada
// paso es una llamada HTTP independiente a un módulo distinto, sin acoplamiento backend
// entre Colaboracion/Analisis/Mensajeria (ver research.md §5).
export function useMarcarInteres() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ licitacionId, nombreLicitacion }: { licitacionId: number; nombreLicitacion: string }) => {
      const interes = (await apiPost<{ data: LicitacionInteres }>(`/api/v1/licitaciones/${licitacionId}/interes`)).data;

      const workspace = (await apiPost<{ data: WorkspaceDetalle }>('/api/v1/analisis/workspaces', {
        licitacionId,
        nombre: `Análisis ${nombreLicitacion}`,
      })).data;

      // participanteIds no puede venir vacío (ConversacionController lo valida, VAL_001) --
      // se incluye al propio usuario que marca el interés como primer asignado; puede sumar
      // más gente después vía el endpoint ya existente de agregar participantes.
      const usuarioActualId = JSON.parse(localStorage.getItem('mpm_user') || '{}').userId;

      const conversacion = interes.conversacionId
        ? null
        : (await apiPost<{ data: ConversacionDetalle }>('/api/v1/conversaciones', {
            tipo: 'grupal',
            asunto: nombreLicitacion,
            licitacionId,
            participanteIds: usuarioActualId ? [String(usuarioActualId)] : [],
          })).data;

      const vinculado = (await apiPatch<{ data: LicitacionInteres }>(`/api/v1/licitaciones/${licitacionId}/interes/vincular`, {
        workspaceId: workspace.id,
        conversacionId: conversacion?.id ?? interes.conversacionId,
      })).data;

      return vinculado;
    },
    onSuccess: (_, { licitacionId }) => {
      queryClient.invalidateQueries({ queryKey: ['licitacion-interes', licitacionId] });
      queryClient.invalidateQueries({ queryKey: ['licitaciones-interes'] });
    },
  });
}
