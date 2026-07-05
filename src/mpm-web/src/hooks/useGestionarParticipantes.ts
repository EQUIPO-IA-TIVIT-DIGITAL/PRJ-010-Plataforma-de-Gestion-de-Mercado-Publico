import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiPost } from '../lib/apiClient';
import type { AgregarParticipanteRequest } from '../types/mensajeria';
import type { ApiResponse } from '../types/api';

export function useGestionarParticipantes() {
  const queryClient = useQueryClient();

  const agregarMutation = useMutation({
    mutationFn: async (data: { conversacionId: number } & AgregarParticipanteRequest) => {
      const json = await apiPost<ApiResponse<unknown>>(`/api/v1/conversaciones/${data.conversacionId}/participantes`, { userId: data.userId, rol: data.rol });
      return json.data;
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['conversacion', variables.conversacionId] });
    },
  });

  const quitarMutation = useMutation({
    mutationFn: async (data: { conversacionId: number; userId: string }) => {
      const json = await apiPost<ApiResponse<unknown>>(`/api/v1/conversaciones/${data.conversacionId}/participantes/${data.userId}/remove`);
      return json.data;
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['conversacion', variables.conversacionId] });
    },
  });

  return {
    agregarParticipante: agregarMutation.mutate,
    quitarParticipante: quitarMutation.mutate,
    isPending: agregarMutation.isPending || quitarMutation.isPending,
  };
}
