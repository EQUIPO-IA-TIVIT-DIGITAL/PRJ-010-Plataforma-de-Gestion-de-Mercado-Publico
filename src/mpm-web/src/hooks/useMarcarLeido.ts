import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiPost } from '../lib/apiClient';
import type { ApiResponse } from '../types/api';

export function useMarcarLeido() {
  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationFn: async (data: { conversacionId: number; mensajeId: number }) => {
      const json = await apiPost<ApiResponse<unknown>>(`/api/v1/conversaciones/${data.conversacionId}/mensajes/${data.mensajeId}/leido`);
      return json.data;
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['mensajes', variables.conversacionId] });
      queryClient.invalidateQueries({ queryKey: ['conversaciones'] });
    },
  });
  return { marcarLeido: mutation.mutate, isPending: mutation.isPending };
}
