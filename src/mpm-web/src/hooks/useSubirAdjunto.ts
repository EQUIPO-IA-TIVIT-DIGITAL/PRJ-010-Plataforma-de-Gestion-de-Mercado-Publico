import { useMutation } from '@tanstack/react-query';
import { apiPost } from '../lib/apiClient';
import type { AdjuntoItem } from '../types/mensajeria';
import type { ApiResponse } from '../types/api';

export function useSubirAdjunto() {
  const mutation = useMutation({
    mutationFn: async ({
      conversacionId,
      mensajeId,
      archivo,
    }: {
      conversacionId: number;
      mensajeId: number;
      archivo: File;
    }) => {
      const formData = new FormData();
      formData.append('archivo', archivo);

      const json = await apiPost<ApiResponse<AdjuntoItem>>(
        `/api/v1/conversaciones/${conversacionId}/mensajes/${mensajeId}/adjuntos`,
        formData,
      );
      return json.data;
    },
  });

  return {
    subirAdjunto: mutation.mutateAsync,
    isUploading: mutation.isPending,
  };
}
