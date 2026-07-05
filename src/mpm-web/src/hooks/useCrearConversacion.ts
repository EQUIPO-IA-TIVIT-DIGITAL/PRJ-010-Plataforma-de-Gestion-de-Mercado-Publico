import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiPost } from '../lib/apiClient';
import type { CrearConversacionRequest, ConversacionDetalle } from '../types/mensajeria';
import type { ApiResponse } from '../types/api';

export function useCrearConversacion() {
  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationFn: async (data: CrearConversacionRequest) => {
      const json = await apiPost<ApiResponse<ConversacionDetalle>>('/api/v1/conversaciones', data);
      return json.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['conversaciones'] });
    },
  });
  return { crearConversacion: mutation.mutate, isPending: mutation.isPending };
}
