import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../lib/apiClient';
import type { ConversacionDetalle } from '../types/mensajeria';
import type { ApiResponse } from '../types/api';

export function useConversacionDetalle(conversacionId: number | null) {
  return useQuery({
    queryKey: ['conversacion', conversacionId],
    queryFn: async () => {
      if (!conversacionId) throw new Error('ID de conversación requerido');
      const json = await apiGet<ApiResponse<ConversacionDetalle>>(`/api/v1/conversaciones/${conversacionId}`);
      return json.data;
    },
    enabled: conversacionId !== null,
    staleTime: 300_000,
  });
}
