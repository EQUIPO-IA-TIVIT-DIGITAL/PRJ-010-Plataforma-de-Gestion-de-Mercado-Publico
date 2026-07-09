import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiDelete, apiGet, apiPatch, apiPost, apiPut } from '../lib/apiClient';
import type { CrearReglaRequest, ProbarAlertaRequest, ProbarAlertaResponse, ReglaAlerta } from '../types/alertas';

const BASE = '/api/v1/alertas';

export function useAlertas() {
  return useQuery({
    queryKey: ['alertas'],
    queryFn: () => apiGet<{ data: ReglaAlerta[] }>(BASE),
  });
}

export function useCrearAlerta() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CrearReglaRequest) => apiPost<{ data: ReglaAlerta }>(BASE, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['alertas'] }),
  });
}

export function useEditarAlerta() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: number; request: CrearReglaRequest }) =>
      apiPut<{ data: unknown }>(`${BASE}/${id}`, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['alertas'] }),
  });
}

export function useToggleAlerta() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => apiPatch<{ data: { activa: boolean } }>(`${BASE}/${id}/toggle`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['alertas'] }),
  });
}

export function useEliminarAlerta() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => apiDelete<{ data: unknown }>(`${BASE}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['alertas'] }),
  });
}

export function useGuardarMiTelegram() {
  return useMutation({
    mutationFn: (telegramChatId: string) =>
      apiPost<{ data: unknown }>(`${BASE}/mi-telegram`, { telegramChatId }),
  });
}

export function useGenerarLinkTelegram() {
  return useMutation({
    mutationFn: () => apiPost<{ data: { url: string } }>(`${BASE}/mi-telegram/link`, {}),
  });
}

export function useGuardarMiEmail() {
  return useMutation({
    mutationFn: (emailAlertas: string) =>
      apiPost<{ data: unknown }>(`${BASE}/mi-email`, { emailAlertas }),
  });
}

export function useProbarAlerta() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: number; request: ProbarAlertaRequest }) =>
      apiPost<{ data: ProbarAlertaResponse }>(`${BASE}/${id}/probar`, request),
    // Sin esto la campanita de notificaciones no se actualiza sola tras disparar una prueba
    // -- hay que refrescar la pagina o esperar a que la query se revalide por su cuenta.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notificaciones'] }),
  });
}
