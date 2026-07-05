import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiPost } from '../lib/apiClient';
import type { EnviarMensajeRequest, MensajeDetalle } from '../types/mensajeria';
import type { ApiResponse } from '../types/api';

export function useEnviarMensaje() {
  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationFn: async (data: { conversacionId: number } & EnviarMensajeRequest) => {
      const json = await apiPost<ApiResponse<MensajeDetalle>>(`/api/v1/conversaciones/${data.conversacionId}/mensajes`, {
        tipo: data.tipo,
        contenido: data.contenido,
        replyToId: data.replyToId,
      });
      return json.data;
    },
    onSuccess: (mensaje, variables) => {
      queryClient.invalidateQueries({ queryKey: ['mensajes', variables.conversacionId] });
      queryClient.invalidateQueries({ queryKey: ['conversaciones'] });
    },
  });
  return { enviarMensaje: mutation.mutateAsync, isPending: mutation.isPending };
}
