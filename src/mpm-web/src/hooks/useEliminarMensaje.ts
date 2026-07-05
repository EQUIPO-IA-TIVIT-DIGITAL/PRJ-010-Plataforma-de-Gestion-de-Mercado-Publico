import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiDelete } from '../lib/apiClient';
import type { ApiResponse } from '../types/api';

export function useEliminarMensaje() {
  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationFn: async (data: { conversacionId: number; mensajeId: number }) => {
      const json = await apiDelete<ApiResponse<unknown>>(`/api/v1/conversaciones/${data.conversacionId}/mensajes/${data.mensajeId}`);
      return json.data;
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['mensajes', variables.conversacionId] });
    },
  });
  return { eliminarMensaje: mutation.mutate, isPending: mutation.isPending };
}
