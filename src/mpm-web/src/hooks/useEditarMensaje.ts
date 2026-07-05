import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiPut } from '../lib/apiClient';
import type { EditarMensajeRequest } from '../types/mensajeria';
import type { ApiResponse } from '../types/api';

export function useEditarMensaje() {
  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationFn: async (data: { conversacionId: number; mensajeId: number } & EditarMensajeRequest) => {
      const json = await apiPut<ApiResponse<unknown>>(`/api/v1/conversaciones/${data.conversacionId}/mensajes/${data.mensajeId}`, { contenido: data.contenido });
      return json.data;
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['mensajes', variables.conversacionId] });
    },
  });
  return { editarMensaje: mutation.mutate, isPending: mutation.isPending };
}
