import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../lib/apiClient';
import type { MensajeDetalle, MensajeFilter } from '../types/mensajeria';
import type { ApiResponse } from '../types/api';

export function useMensajes(conversacionId: number | null, params: MensajeFilter) {
  return useQuery({
    queryKey: ['mensajes', conversacionId, params],
    queryFn: async () => {
      if (!conversacionId) throw new Error('ID de conversación requerido');
      const qs = new URLSearchParams({
        page: params.page.toString(),
        pageSize: params.pageSize.toString(),
      });
      if (params.before) qs.append('before', params.before.toString());

      const json = await apiGet<ApiResponse<{ items: MensajeDetalle[]; page: number; pageSize: number; totalRecords: number; totalPages: number }>>(`/api/v1/conversaciones/${conversacionId}/mensajes?${qs}`);
      return json.data;
    },
    enabled: conversacionId !== null,
    staleTime: 30_000,
  });
}
